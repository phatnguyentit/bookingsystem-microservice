using System.Text.Json;

namespace BookingSystem.PaymentService.Infrastructure.Outbox;

/// <summary>
/// A pending integration event awaiting publication to Kafka. The row is written in the
/// same transaction as the payment state change, so the persisted outcome and the emitted
/// event can never diverge; <c>PaymentOutboxProcessor</c> publishes them out-of-band.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Topic { get; private set; } = default!;       // Kafka topic to publish to
    public string EventType { get; private set; } = default!;   // integration event type name (discriminator)
    public string Payload { get; private set; } = default!;     // JSON-serialized event
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage For<T>(string topic, T @event) where T : class => new()
    {
        Id = Guid.NewGuid(),
        Topic = topic,
        EventType = typeof(T).Name,
        Payload = JsonSerializer.Serialize(@event),
        CreatedAt = DateTime.UtcNow
    };

    public void MarkProcessed() => ProcessedAt = DateTime.UtcNow;

    public void MarkFailed(string error) => Error = error;
}
