using BookingSystem.PaymentService.Api.Consumers;
using BookingSystem.PaymentService.Api.Features.ProcessPayment;
using BookingSystem.PaymentService.Api.Features.RefundPayment;
using BookingSystem.Shared.Contracts.Events.Bookings;
using BookingSystem.Shared.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace PaymentService.Tests.Consumers;

public class PaymentKafkaConsumersTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IServiceScopeFactory _scopeFactory;

    public PaymentKafkaConsumersTests()
    {
        // Real DI container with the substitute registered, so the consumer's
        // scope-resolution path (CreateAsyncScope + GetRequiredService) is exercised too.
        _scopeFactory = new ServiceCollection()
            .AddScoped(_ => _sender)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
    }

    private sealed class TestableBookingCreatedConsumer(IServiceScopeFactory scopeFactory)
        : BookingCreatedPaymentConsumer(
            Options.Create(new KafkaServerSettings()),
            NullLogger<BookingCreatedPaymentConsumer>.Instance,
            scopeFactory)
    {
        public Task Process(BookingCreatedIntegrationEvent message) => ProcessAsync(message, default);
    }

    private sealed class TestableConfirmationFailedConsumer(IServiceScopeFactory scopeFactory)
        : BookingConfirmationFailedPaymentConsumer(
            Options.Create(new KafkaServerSettings()),
            NullLogger<BookingConfirmationFailedPaymentConsumer>.Instance,
            scopeFactory)
    {
        public Task Process(BookingConfirmationFailedIntegrationEvent message) => ProcessAsync(message, default);
    }

    [Fact]
    public async Task BookingCreated_DispatchesProcessPaymentCommandWithCardMethod()
    {
        var @event = new BookingCreatedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5),
            400m, "USD", DateTimeOffset.UtcNow);

        await new TestableBookingCreatedConsumer(_scopeFactory).Process(@event);

        await _sender.Received(1).Send(
            new ProcessPaymentCommand(@event.BookingId, @event.UserId, 400m, "USD", "Card"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BookingConfirmationFailed_DispatchesRefundPaymentCommand()
    {
        var @event = new BookingConfirmationFailedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), "booking already cancelled", DateTimeOffset.UtcNow);

        await new TestableConfirmationFailedConsumer(_scopeFactory).Process(@event);

        await _sender.Received(1).Send(
            new RefundPaymentCommand(@event.BookingId, "booking already cancelled"),
            Arg.Any<CancellationToken>());
    }
}
