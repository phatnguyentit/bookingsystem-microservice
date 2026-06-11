---
name: Feature story
about: A net-new capability spanning aggregate/endpoint/event work, often across services.
title: "feat: <short summary>"
labels: enhancement
assignees: ""
---

## Business Context
<!-- Why this matters to a user/business. The current workaround and its cost. -->

## Problem Statement
- <!-- Concrete missing piece — e.g. no `PUT /api/bookings/{id}` endpoint exists. -->
- <!-- Missing domain event / consumer / etc. Cite file_path:line where relevant. -->
- <!-- Side effects of the gap today. -->

## Proposed Solution

### <PrimaryService> changes
- <!-- Domain event / aggregate method, with its guards and what it raises. -->
- <!-- Command + handler (MediatR), endpoint shape and request body. -->

### <OtherService(s)> changes
- <!-- Kafka event to publish/consume, notification, availability update, etc. -->

## Acceptance Criteria

- [ ] <!-- observable, testable outcome — include expected HTTP codes -->
- [ ] <!-- domain event persisted via outbox and published to Kafka -->
- [ ] <!-- existing create/cancel flows are unaffected -->

## Effort Estimate

**<N–M days>**
- Day 1: <slice>
- Day 2: <slice>
- Day 3: <slice>

## Notes / References
- Related: #<issue>, `.claude/rules/modules/<x>.md`
- Affected service(s): <ServiceName>
