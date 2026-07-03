# Booking System — Supported Scenarios

> End-to-end behaviour of the booking saga across BookingService, PaymentService, and
> NotificationService. Focuses on the **payment ↔ booking confirmation saga** and its failure
> handling (issue #36). All cross-service messaging is Kafka choreography with at-least-once
> delivery; producer reliability comes from the **outbox** and consumer reliability from
> `KafkaConsumerBase` (manual offset commit, bounded retry, dead-letter topic).

---

## Actors & topics

| Topic | Producer | Consumer(s) |
|---|---|---|
| `booking.created` | BookingService (outbox) | PaymentService, NotificationService |
| `payment.succeeded` | PaymentService (outbox) | BookingService, NotificationService |
| `payment.failed` | PaymentService (outbox) | BookingService, NotificationService |
| `booking.cancelled` | BookingService (outbox) | _(no consumer yet)_ |
| `booking.confirmation.failed` | BookingService (outbox) | PaymentService |
| `payment.refunded` | PaymentService (outbox) | _(no consumer yet)_ |
| `payment.refund.failed` | PaymentService (outbox) | _(no consumer yet — operator alert)_ |

Every topic has a matching `<topic>.dlq` provisioned in `AppHost/Program.cs`. A consumer parks a
message there on a permanent failure or after retries are exhausted, then commits the offset so the
partition is never blocked. **Exception: a refund is never dead-lettered** — see scenario 4.

`Booking` aggregate states: `Pending → Confirmed → Completed`, or `→ Cancelled` (terminal).
`Payment` states: `Pending → Succeeded → RefundPending → Refunded`, with `→ Failed` (charge declined)
and `RefundPending → RefundFailed` (refund permanently declined) as terminal branches.

---

## 1. Happy path — booking created, paid, confirmed

```
Client → POST /api/bookings
  → Booking.Create()            Status = Pending
  → outbox → booking.created
        │
        ├── PaymentService: BookingCreatedPaymentConsumer → ProcessPaymentCommand
        │     → gateway charge OK → Payment.Status = Succeeded
        │     → outbox → payment.succeeded
        │           │
        │           └── BookingService: PaymentSucceededKafkaConsumer → ConfirmBookingCommand
        │                 → booking is Pending → Booking.Confirm()   Status = Confirmed
        │                 → outbox → booking.confirmed (internal; no Kafka handler)
        │
        └── NotificationService: BookingCreatedKafkaConsumer → "booking created" email
              + PaymentSucceededKafkaConsumer → "payment succeeded" email
```

**Result:** booking `Confirmed`, payment `Succeeded`, two emails logged. Offsets committed normally.

---

## 2. Payment declined — booking cancelled

```
booking.created → ProcessPaymentCommand → gateway declines
  → Payment.Status = Failed
  → outbox → payment.failed
        │
        ├── BookingService: PaymentFailedKafkaConsumer → CancelBookingCommand
        │     → Booking.Cancel(reason)   Status = Cancelled
        │     → outbox → booking.cancelled
        │
        └── NotificationService: PaymentFailedKafkaConsumer → "payment failed" email
```

**Result:** booking `Cancelled`, payment `Failed`. No money captured.

---

## 3. Duplicate `payment.succeeded` (at-least-once redelivery)

The same `payment.succeeded` is delivered twice for a booking already `Confirmed`.

```
payment.succeeded (redelivery) → ConfirmBookingCommand
  → booking.Status == Confirmed → handler returns (no-op)
  → no Confirm(), no DB write, no outbox row
  → consumer commits the offset once
```

**Result:** booking stays `Confirmed`, no exception, no DLQ. *Idempotent.*
(Symmetric: a duplicate `payment.failed` on an already-`Cancelled` booking is also a no-op.)

---

## 4. Saga conflict — payment captured but booking can't be confirmed

A `payment.succeeded` arrives for a booking that is already `Cancelled` (user cancelled mid-flight,
or a `payment.failed` got there first). The aggregate forbids `Cancelled → Confirmed`, so we must
**not** silently drop it — money was captured. Instead we compensate.

```
payment.succeeded → ConfirmBookingCommand
  → booking.Status != Pending and != Confirmed (it's Cancelled/Completed)
  → Booking.RejectConfirmation(reason)      // does NOT change Status
  → outbox → booking.confirmation.failed     // compensation signal
  → consumer commits the offset (no throw, no retry, no DLQ)
        │
        └── PaymentService: BookingConfirmationFailedPaymentConsumer → RefundPaymentCommand
              → find captured payment → Payment.Status = RefundPending   (records the debt; always commits)
                    │
              RefundProcessor (background, every 5s) polls RefundPending:
                    ├─ gateway OK       → Refunded     → outbox → payment.refunded
                    ├─ gateway throws   → stays RefundPending → retried forever (never dropped)
                    └─ gateway declines → RefundFailed → outbox → payment.refund.failed + operator alert
```

**A refund is a financial obligation, not a discardable message**, so it is handled differently from
every other consumer path:

- The consumer only **records the obligation** (`RefundPending`) — a single DB write that can't fail
  in a way that needs a dead-letter. **Refunds never go to a DLQ.**
- `RefundProcessor` (an outbox-style reconciler) drives `RefundPending` to completion, retrying
  **transient** gateway failures indefinitely so the money is never abandoned.
- A **permanent** gateway decline → `RefundFailed` + `payment.refund.failed` + a `LogCritical`
  operator alert, because only a human can resolve it.

> `RejectConfirmation` does **not** reject the payment — it records that confirmation failed
> *despite* a successful payment, which is precisely the refund trigger.

---

## 5. Duplicate `booking.confirmation.failed` (refund idempotency)

```
booking.confirmation.failed (redelivery) → RefundPaymentCommand
  → captured payment is already RefundPending / Refunded / RefundFailed → handler returns (no-op)
  → no duplicate obligation, no second refund, no reset of a RefundFailed payment
  → consumer commits the offset
```

**Result:** the obligation is recorded once and the gateway is reversed exactly once (the
`PaymentId` is the gateway idempotency key). *Idempotent.* A redelivery against any terminal/in-flight
refund state — including `RefundFailed` (awaiting a manual refund) — is a clean no-op, never a reset
back to `RefundPending`. *(Only a `Succeeded` payment with no refund yet triggers a new obligation.)*

---

## 6. Failure classification at the consumer

`KafkaConsumerBase` decides what to do with an exception thrown out of `ProcessAsync`:

| Failure | Example | Behaviour |
|---|---|---|
| **Transient** | DB unavailable, network blip | `Seek` back + retry up to `maxAttempts` (default 3, exponential backoff); offset **not** advanced. Then DLQ. |
| **Permanent (poison)** | `NotFoundException` (`: IPermanentMessageException`), malformed JSON | Skip the retry budget → dead-letter immediately → commit offset. |
| **Business compensation** | captured payment, booking unconfirmable | Not an exception — handler emits a compensation event (case 4) and returns; offset committed. |

The key invariant (issue #36): **a failed message's offset is never leapfrogged by a later
message's commit.** A failure either rewinds-and-retries or is dead-lettered-then-committed.

---

## 7. Edge cases

- **Booking not found on confirm** — `ConfirmBookingHandler` throws `NotFoundException`, which is
  permanent → dead-lettered immediately (no wasted retries).
- **No captured payment on refund** — `RefundPaymentHandler` logs a warning and returns (retrying
  can't make a payment appear); offset committed. Unexpected, since the compensation event only
  fires after a `payment.succeeded`.
- **Gateway down during refund** — the payment stays `RefundPending` and `RefundProcessor` retries
  every cycle indefinitely; after `TransientAlertThreshold` consecutive failures it raises a
  `LogCritical` alert while still retrying. The debt is never dropped.
- **Refund permanently declined** — `RefundProcessor` moves the payment to `RefundFailed`, emits
  `payment.refund.failed`, and raises a `LogCritical` operator alert so a human issues a manual refund.
- **Kafka down when publishing** — outbox rows stay unprocessed; the outbox processor retries on its
  next poll. The DB state and the eventual event can't diverge.

---

## Not yet supported (open follow-ups)

- No consumer for `payment.refunded` (no "you've been refunded" email) or `payment.refund.failed`
  (no ticketing/ops-workflow consumer — the alert is a `LogCritical` for now).
- **OpenTelemetry metrics for refund alerts are deferred** — the `RefundFailed` / stuck-refund alerts
  are logs only; emitting `payments.refund.failed` / `payments.refund.stuck` counters (so dashboards
  and alerting fire on the rate) is intentionally left for a separate issue. See the `TODO(metrics)`
  markers in `RefundProcessor`.
- No consumer for `booking.cancelled`.
- `RefundProcessor` uses a generic refund reason (the per-booking reason isn't persisted on the
  payment — would need a `RefundReason` column).
- Transient-refund attempt counts are tracked in-memory (reset on restart); no persisted retry/SLA
  history.
- Integration tests (Testcontainers Kafka) for the saga — see `.claude/rules/testing.md`.
