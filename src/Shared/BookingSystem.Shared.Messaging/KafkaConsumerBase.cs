using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace BookingSystem.Shared.Messaging;

public abstract class KafkaConsumerBase<T>(string topic, string groupId, IOptions<KafkaServerSettings> kafkaSettings, ILogger logger, int maxAttempts = 3) : BackgroundService where T : class
{
    protected abstract Task ProcessAsync(T message, CancellationToken cancellationToken);

    // Failed messages are parked here. Keep this suffix in sync with the dead-letter
    // topics provisioned in AppHost/Program.cs.
    private string DeadLetterTopic => $"{topic}.dlq";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        using var deadLetterProducer = new ProducerBuilder<string, string>(
            new ProducerConfig { BootstrapServers = kafkaSettings.Value.BootstrapServers }).Build();

        consumer.Subscribe(topic);

        // Tracks consecutive failures for the offset we're currently retrying.
        // Assumes one partition per consumer (topics are provisioned with NumPartitions = 1);
        // for multiple partitions, key the attempt count by TopicPartitionOffset instead.
        TopicPartitionOffset? retrying = null;
        var attempts = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Consume a message from the topic
                // - Broker: Last committed offset is N-1, consumer_offset N
                // - Consumer: in-memory offset is now N, but it will not commit it to the broker until we call Commit().
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                // Same offset as the last failure => this is a retry; otherwise a fresh message.
                attempts = retrying is not null && retrying.Equals(result.TopicPartitionOffset)
                    ? attempts + 1
                    : 1;

                T? message;
                try
                {
                    message = JsonSerializer.Deserialize<T>(result.Message.Value);
                }
                catch (JsonException ex)
                {
                    // Malformed payload: retrying can never succeed -> straight to dead-letter.
                    logger.LogError(ex,
                        "Could not deserialize message at {Offset} from {Topic}; sending to dead-letter",
                        result.TopicPartitionOffset, topic);
                    await SendToDeadLetterAsync(deadLetterProducer, result, ex, attempts, stoppingToken);

                    // Consumer: Commit the offset to the broker so we don't retry this message again.
                    // Broker: Last committed offset will be updated.
                    consumer.Commit(result);
                    retrying = null;
                    continue;
                }

                try
                {
                    if (message is not null)
                        await ProcessAsync(message, stoppingToken);

                    // Advance the cursor only after successful processing. If ProcessAsync throws, we will retry
                    // - This consumer will the offset N+1 to broker
                    consumer.Commit(result);
                    retrying = null;   // success clears the retry state
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;   // shutting down — let the outer handler close the consumer
                }
                catch (Exception ex) when (ex is IPermanentMessageException)
                {
                    // Poison message: retrying can never succeed -> skip the retry budget and
                    // dead-letter immediately, then commit so the partition is not blocked.
                    logger.LogError(ex,
                        "Permanent failure for {Offset} on {Topic}; sending to dead-letter",
                        result.TopicPartitionOffset, topic);
                    await SendToDeadLetterAsync(deadLetterProducer, result, ex, attempts, stoppingToken);
                    consumer.Commit(result);
                    retrying = null;
                }
                catch (Exception ex)
                {
                    if (attempts < maxAttempts)
                    {
                        // Assume a transient fault: rewind the cursor and retry after a short backoff.
                        var backoff = TimeSpan.FromMilliseconds(200 * attempts);
                        logger.LogWarning(ex,
                            "Processing failed (attempt {Attempt}/{Max}) for {Offset} on {Topic}; retrying in {Backoff}ms",
                            attempts, maxAttempts, result.TopicPartitionOffset, topic, backoff.TotalMilliseconds);

                        await Task.Delay(backoff, stoppingToken);
                        // - This consumer will reset the offset to N, so the next Consume() will return the same message again.
                        consumer.Seek(result.TopicPartitionOffset);
                        retrying = result.TopicPartitionOffset;
                    }
                    else
                    {
                        // Retries exhausted: park in the dead-letter topic and move past it.
                        logger.LogError(ex,
                            "Processing failed after {Max} attempts for {Offset} on {Topic}; sending to dead-letter",
                            maxAttempts, result.TopicPartitionOffset, topic);
                        await SendToDeadLetterAsync(deadLetterProducer, result, ex, attempts, stoppingToken);
                        consumer.Commit(result);
                        retrying = null;
                    }
                }
            }
        }
        catch (OperationCanceledException) { /* graceful shutdown */ }
        finally
        {
            consumer.Close();
        }
    }

    private async Task SendToDeadLetterAsync(
        IProducer<string, string> producer,
        ConsumeResult<string, string> result,
        Exception error,
        int attempts,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = result.Message.Key,
                Value = result.Message.Value,
                Headers = new Headers
                {
                    { "x-original-topic", Encoding.UTF8.GetBytes(topic) },
                    { "x-original-offset", Encoding.UTF8.GetBytes(result.TopicPartitionOffset.ToString()) },
                    { "x-attempts", Encoding.UTF8.GetBytes(attempts.ToString()) },
                    { "x-exception", Encoding.UTF8.GetBytes(error.Message) },
                }
            };

            await producer.ProduceAsync(DeadLetterTopic, message, cancellationToken);
            logger.LogInformation(
                "Dead-lettered message from {Offset} to {DeadLetterTopic}",
                result.TopicPartitionOffset, DeadLetterTopic);
        }
        catch (Exception ex)
        {
            // If we can't even dead-letter, log loudly. The original is still committed by the
            // caller to avoid blocking the partition; the payload is preserved in the log above.
            logger.LogCritical(ex,
                "Failed to publish to dead-letter topic {DeadLetterTopic}; message at {Offset} will be dropped",
                DeadLetterTopic, result.TopicPartitionOffset);
        }
    }
}

