using BookingSystem.BookingService.Api.Consumers;
using BookingSystem.BookingService.Application.Commands.CancelBooking;
using BookingSystem.BookingService.Application.Commands.ConfirmBooking;
using BookingSystem.Shared.Contracts.Events.Payments;
using BookingSystem.Shared.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BookingService.Api.Tests.Consumers;

public class BookingKafkaConsumersTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IServiceScopeFactory _scopeFactory;

    public BookingKafkaConsumersTests()
    {
        _scopeFactory = new ServiceCollection()
            .AddScoped(_ => _sender)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
    }

    private sealed class TestablePaymentSucceededConsumer(IServiceScopeFactory scopeFactory)
        : PaymentSucceededKafkaConsumer(
            Options.Create(new KafkaServerSettings()),
            NullLogger<PaymentSucceededKafkaConsumer>.Instance,
            scopeFactory)
    {
        public Task Process(PaymentSucceededIntegrationEvent message) => ProcessAsync(message, default);
    }

    private sealed class TestablePaymentFailedConsumer(IServiceScopeFactory scopeFactory)
        : PaymentFailedKafkaConsumer(
            Options.Create(new KafkaServerSettings()),
            NullLogger<PaymentFailedKafkaConsumer>.Instance,
            scopeFactory)
    {
        public Task Process(PaymentFailedIntegrationEvent message) => ProcessAsync(message, default);
    }

    [Fact]
    public async Task PaymentSucceeded_DispatchesConfirmBookingCommand()
    {
        var @event = new PaymentSucceededIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 400m, "USD", DateTimeOffset.UtcNow);

        await new TestablePaymentSucceededConsumer(_scopeFactory).Process(@event);

        await _sender.Received(1).Send(
            new ConfirmBookingCommand(@event.BookingId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PaymentFailed_DispatchesCancelBookingCommandWithReason()
    {
        var @event = new PaymentFailedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "insufficient funds", DateTimeOffset.UtcNow);

        await new TestablePaymentFailedConsumer(_scopeFactory).Process(@event);

        await _sender.Received(1).Send(
            new CancelBookingCommand(@event.BookingId, "Payment failed: insufficient funds"),
            Arg.Any<CancellationToken>());
    }
}
