using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BookingSystem.Shared.Messaging;

public abstract class KafkaConsumerBase<T>(
    string topic,
    string groupId,
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger logger) : BackgroundService where T : class
{
    protected abstract Task ProcessAsync(T message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                try
                {
                    var message = JsonSerializer.Deserialize<T>(result.Message.Value);
                    if (message is not null)
                        await ProcessAsync(message, stoppingToken);
                    consumer.Commit(result);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing message from topic {Topic}", topic);
                }
            }
        }
        catch (OperationCanceledException) { /* graceful shutdown */ }
        finally
        {
            consumer.Close();
        }
    }
}
