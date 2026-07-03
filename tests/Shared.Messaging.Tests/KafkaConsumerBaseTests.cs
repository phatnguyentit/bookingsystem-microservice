using System.Text;
using System.Text.Json;
using BookingSystem.Shared.Messaging;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Shared.Messaging.Tests;

public class KafkaConsumerBaseTests
{
    private const string Topic = "test.topic";
    private const string DeadLetterTopic = "test.topic.dlq";

    private readonly IConsumer<string, string> _consumer = Substitute.For<IConsumer<string, string>>();
    private readonly IProducer<string, string> _producer = Substitute.For<IProducer<string, string>>();
    private readonly Queue<ConsumeResult<string, string>> _pending = new();
    private readonly List<TestMessage> _processed = [];

    public KafkaConsumerBaseTests()
    {
        // Serve queued results; when drained, signal shutdown the same way a cancelled
        // Consume would, so ExecuteAsync exits its loop gracefully.
        _consumer.Consume(Arg.Any<CancellationToken>()).Returns(_ =>
            _pending.Count > 0 ? _pending.Dequeue() : throw new OperationCanceledException());

        _producer.ProduceAsync(Arg.Any<string>(), Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeliveryResult<string, string>()));
    }

    public record TestMessage(string Text);

    private class PermanentFailureException(string message)
        : Exception(message), IPermanentMessageException;

    private sealed class TestConsumer(
        IConsumer<string, string> consumer,
        IProducer<string, string> producer,
        Func<TestMessage, CancellationToken, Task> process,
        int maxAttempts = 3)
        : KafkaConsumerBase<TestMessage>(
            Topic, "test-group",
            Options.Create(new KafkaServerSettings()),
            NullLogger.Instance,
            maxAttempts)
    {
        protected override IConsumer<string, string> CreateConsumer(ConsumerConfig config) => consumer;
        protected override IProducer<string, string> CreateDeadLetterProducer(ProducerConfig config) => producer;
        protected override Task ProcessAsync(TestMessage message, CancellationToken cancellationToken)
            => process(message, cancellationToken);
        public Task RunAsync(CancellationToken cancellationToken = default) => ExecuteAsync(cancellationToken);
    }

    private static ConsumeResult<string, string> Result(string value, long offset) => new()
    {
        Message = new Message<string, string> { Key = "booking-1", Value = value },
        TopicPartitionOffset = new TopicPartitionOffset(Topic, new Partition(0), new Offset(offset)),
    };

    /// <summary>Queues the same offset <paramref name="copies"/> times — a Seek-rewind redelivery.</summary>
    private void Enqueue(string value, long offset, int copies = 1)
    {
        for (var i = 0; i < copies; i++) _pending.Enqueue(Result(value, offset));
    }

    private static string Json(string text) => JsonSerializer.Serialize(new TestMessage(text));

    private TestConsumer CreateSucceedingConsumer() =>
        new(_consumer, _producer, (m, _) => { _processed.Add(m); return Task.CompletedTask; });

    [Fact]
    public async Task ValidMessage_IsProcessedAndCommitted()
    {
        Enqueue(Json("hello"), offset: 5);

        await CreateSucceedingConsumer().RunAsync();

        _processed.Should().Equal(new TestMessage("hello"));
        _consumer.Received(1).Subscribe(Topic);
        _consumer.Received(1).Commit(Arg.Is<ConsumeResult<string, string>>(r => r.Offset == 5));
        await _producer.DidNotReceive().ProduceAsync(
            Arg.Any<string>(), Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MalformedPayload_SkipsProcessingAndDeadLettersImmediately()
    {
        Enqueue("{ not json !", offset: 0);

        await CreateSucceedingConsumer().RunAsync();

        _processed.Should().BeEmpty();
        await _producer.Received(1).ProduceAsync(
            DeadLetterTopic, Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>());
        _consumer.Received(1).Commit(Arg.Any<ConsumeResult<string, string>>());
        _consumer.DidNotReceive().Seek(Arg.Any<TopicPartitionOffset>());
    }

    [Fact]
    public async Task TransientFailure_RewindsAndRetries_ThenCommitsOnSuccess()
    {
        // Fails twice, succeeds on the 3rd attempt (within maxAttempts = 3)
        var attempts = 0;
        var consumer = new TestConsumer(_consumer, _producer, (m, _) =>
        {
            attempts++;
            if (attempts < 3) throw new InvalidOperationException("transient");
            _processed.Add(m);
            return Task.CompletedTask;
        });
        Enqueue(Json("retry-me"), offset: 7, copies: 3); // initial + 2 redeliveries after Seek

        await consumer.RunAsync();

        attempts.Should().Be(3);
        _processed.Should().Equal(new TestMessage("retry-me"));
        _consumer.Received(2).Seek(new TopicPartitionOffset(Topic, new Partition(0), new Offset(7)));
        _consumer.Received(1).Commit(Arg.Is<ConsumeResult<string, string>>(r => r.Offset == 7));
        await _producer.DidNotReceive().ProduceAsync(
            Arg.Any<string>(), Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransientFailure_RetriesExhausted_DeadLettersWithDiagnosticHeaders()
    {
        var consumer = new TestConsumer(_consumer, _producer,
            (_, _) => throw new InvalidOperationException("still broken"), maxAttempts: 3);
        Enqueue(Json("poison"), offset: 9, copies: 3);

        Message<string, string>? deadLettered = null;
        await _producer.ProduceAsync(
            DeadLetterTopic,
            Arg.Do<Message<string, string>>(m => deadLettered = m),
            Arg.Any<CancellationToken>());

        await consumer.RunAsync();

        _consumer.Received(2).Seek(Arg.Any<TopicPartitionOffset>());   // attempts 1 and 2 rewind
        _consumer.Received(1).Commit(Arg.Any<ConsumeResult<string, string>>()); // parked, then committed
        deadLettered.Should().NotBeNull();
        deadLettered!.Key.Should().Be("booking-1");
        deadLettered.Value.Should().Be(Json("poison")); // original payload preserved for replay
        Encoding.UTF8.GetString(deadLettered.Headers.GetLastBytes("x-original-topic")).Should().Be(Topic);
        Encoding.UTF8.GetString(deadLettered.Headers.GetLastBytes("x-attempts")).Should().Be("3");
        Encoding.UTF8.GetString(deadLettered.Headers.GetLastBytes("x-exception")).Should().Be("still broken");
    }

    [Fact]
    public async Task PermanentException_SkipsRetryBudget_DeadLettersOnFirstAttempt()
    {
        var attempts = 0;
        var consumer = new TestConsumer(_consumer, _producer, (_, _) =>
        {
            attempts++;
            throw new PermanentFailureException("aggregate not found");
        });
        Enqueue(Json("gone"), offset: 3);

        await consumer.RunAsync();

        attempts.Should().Be(1, "a permanent failure must not consume the retry budget");
        _consumer.DidNotReceive().Seek(Arg.Any<TopicPartitionOffset>());
        await _producer.Received(1).ProduceAsync(
            DeadLetterTopic, Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>());
        _consumer.Received(1).Commit(Arg.Any<ConsumeResult<string, string>>());
    }

    [Fact]
    public async Task DeadLetterPublishFails_DoesNotCommit_RewindsAndRetriesUntilParked()
    {
        var consumer = new TestConsumer(_consumer, _producer,
            (_, _) => throw new PermanentFailureException("poison"));
        Enqueue(Json("stuck"), offset: 4, copies: 2); // first DLQ attempt fails -> redelivered once

        // DLQ publish: broker down on the first attempt, recovers on the second
        _producer.ProduceAsync(DeadLetterTopic, Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new Exception("broker down"),
                _ => Task.FromResult(new DeliveryResult<string, string>()));

        await consumer.RunAsync();

        // Never committed while unparked (would drop the message), rewound instead,
        // then committed exactly once after the DLQ publish finally succeeded.
        _consumer.Received(1).Seek(new TopicPartitionOffset(Topic, new Partition(0), new Offset(4)));
        _consumer.Received(1).Commit(Arg.Any<ConsumeResult<string, string>>());
        await _producer.Received(2).ProduceAsync(
            DeadLetterTopic, Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>());
    }
}
