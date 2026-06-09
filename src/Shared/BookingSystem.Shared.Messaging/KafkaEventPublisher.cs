using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BookingSystem.Shared.Messaging;

public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(string bootstrapServers, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;
        var config = new ProducerConfig { BootstrapServers = bootstrapServers };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<T>(string topic, T @event, CancellationToken cancellationToken = default)
        where T : class
    {
        var value = JsonSerializer.Serialize(@event);
        var message = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = value
        };

        try
        { 
            var result = await _producer.ProduceAsync(topic, message, cancellationToken);
            _logger.LogInformation(
                "Published {EventType} to topic {Topic} at offset {Offset}",
                typeof(T).Name, topic, result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish {EventType} to topic {Topic}", typeof(T).Name, topic);
            throw;
        }
    }

    public void Dispose() => _producer.Dispose();
}
