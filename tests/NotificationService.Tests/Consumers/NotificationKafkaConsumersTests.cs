using BookingSystem.NotificationService.Api.Consumers;
using BookingSystem.NotificationService.Infrastructure.Services;
using BookingSystem.Shared.Contracts.Events.Bookings;
using BookingSystem.Shared.Contracts.Events.Payments;
using BookingSystem.Shared.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace NotificationService.Tests.Consumers;

public class NotificationKafkaConsumersTests
{
    private readonly INotificationSender _emailSender = Substitute.For<INotificationSender>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly List<(Guid RecipientId, string Message)> _sent = [];

    public NotificationKafkaConsumersTests()
    {
        _emailSender.SendEmailAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => _sent.Add((call.ArgAt<Guid>(0), call.ArgAt<string>(1))));

        _scopeFactory = new ServiceCollection()
            .AddScoped(_ => _emailSender)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
    }

    private static IOptions<KafkaServerSettings> Settings => Options.Create(new KafkaServerSettings());

    private sealed class TestableBookingCreatedConsumer(IServiceScopeFactory sf)
        : BookingCreatedKafkaConsumer(Settings, NullLogger<BookingCreatedKafkaConsumer>.Instance, sf)
    {
        public Task Process(BookingCreatedIntegrationEvent m) => ProcessAsync(m, default);
    }

    private sealed class TestableBookingCancelledConsumer(IServiceScopeFactory sf)
        : BookingCancelledKafkaConsumer(Settings, NullLogger<BookingCancelledKafkaConsumer>.Instance, sf)
    {
        public Task Process(BookingCancelledIntegrationEvent m) => ProcessAsync(m, default);
    }

    private sealed class TestablePaymentSucceededConsumer(IServiceScopeFactory sf)
        : PaymentSucceededKafkaConsumer(Settings, NullLogger<PaymentSucceededKafkaConsumer>.Instance, sf)
    {
        public Task Process(PaymentSucceededIntegrationEvent m) => ProcessAsync(m, default);
    }

    private sealed class TestablePaymentFailedConsumer(IServiceScopeFactory sf)
        : PaymentFailedKafkaConsumer(Settings, NullLogger<PaymentFailedKafkaConsumer>.Instance, sf)
    {
        public Task Process(PaymentFailedIntegrationEvent m) => ProcessAsync(m, default);
    }

    [Fact]
    public async Task BookingCreated_EmailsTheBookingUser()
    {
        var @event = new BookingCreatedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 400m, "USD", DateTimeOffset.UtcNow);

        await new TestableBookingCreatedConsumer(_scopeFactory).Process(@event);

        _sent.Should().ContainSingle();
        _sent[0].RecipientId.Should().Be(@event.UserId);
        _sent[0].Message.Should().Contain(@event.BookingId.ToString()).And.Contain("created");
    }

    [Fact]
    public async Task BookingCancelled_EmailIncludesCancellationReason()
    {
        var @event = new BookingCancelledIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "host unavailable", DateTimeOffset.UtcNow);

        await new TestableBookingCancelledConsumer(_scopeFactory).Process(@event);

        _sent.Should().ContainSingle();
        _sent[0].RecipientId.Should().Be(@event.UserId);
        _sent[0].Message.Should().Contain("cancelled").And.Contain("host unavailable");
    }

    [Fact]
    public async Task PaymentSucceeded_EmailIncludesAmountAndCurrency()
    {
        var @event = new PaymentSucceededIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 400m, "USD", DateTimeOffset.UtcNow);

        await new TestablePaymentSucceededConsumer(_scopeFactory).Process(@event);

        _sent.Should().ContainSingle();
        _sent[0].RecipientId.Should().Be(@event.UserId);
        _sent[0].Message.Should().Contain("400").And.Contain("USD").And.Contain("succeeded");
    }

    [Fact]
    public async Task PaymentFailed_EmailIncludesFailureReason()
    {
        var @event = new PaymentFailedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "insufficient funds", DateTimeOffset.UtcNow);

        await new TestablePaymentFailedConsumer(_scopeFactory).Process(@event);

        _sent.Should().ContainSingle();
        _sent[0].RecipientId.Should().Be(@event.UserId);
        _sent[0].Message.Should().Contain("failed").And.Contain("insufficient funds");
    }
}
