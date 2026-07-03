using BookingSystem.BookingService.Application.EventHandlers;
using BookingSystem.BookingService.Domain.Events;
using BookingSystem.BookingService.Domain.ValueObjects;
using BookingSystem.Shared.Contracts.Events;
using BookingSystem.Shared.Contracts.Events.Bookings;
using BookingSystem.Shared.Messaging;
using FluentAssertions;
using NSubstitute;

namespace BookingService.Application.Tests.EventHandlers;

public class PublishBookingCreatedHandlerTests
{
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();

    [Fact]
    public async Task Handle_PublishesIntegrationEventToBookingCreatedTopic()
    {
        var domainEvent = BookingCreatedEvent.Create(
            BookingId.New(),
            new UserId(Guid.NewGuid()),
            new CatalogId(Guid.NewGuid()),
            new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5)),
            new Money(400m, "USD"));

        BookingCreatedIntegrationEvent? published = null;
        await _publisher.PublishAsync(
            KafkaTopics.BookingCreated,
            Arg.Do<BookingCreatedIntegrationEvent>(e => published = e),
            Arg.Any<CancellationToken>());

        await new PublishBookingCreatedHandler(_publisher).Handle(domainEvent, default);

        published.Should().NotBeNull();
        published!.BookingId.Should().Be(domainEvent.BookingId.Value);
        published.UserId.Should().Be(domainEvent.UserId.Value);
        published.CatalogId.Should().Be(domainEvent.CatalogId.Value);
        published.CheckIn.Should().Be(domainEvent.Period.CheckIn);
        published.CheckOut.Should().Be(domainEvent.Period.CheckOut);
        published.Amount.Should().Be(400m);
        published.Currency.Should().Be("USD");
        published.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}

public class PublishBookingCancelledHandlerTests
{
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();

    [Fact]
    public async Task Handle_PublishesIntegrationEventToBookingCancelledTopic()
    {
        var domainEvent = BookingCancelledEvent.Create(
            BookingId.New(),
            new UserId(Guid.NewGuid()),
            new CatalogId(Guid.NewGuid()),
            "changed my mind");

        BookingCancelledIntegrationEvent? published = null;
        await _publisher.PublishAsync(
            KafkaTopics.BookingCancelled,
            Arg.Do<BookingCancelledIntegrationEvent>(e => published = e),
            Arg.Any<CancellationToken>());

        await new PublishBookingCancelledHandler(_publisher).Handle(domainEvent, default);

        published.Should().NotBeNull();
        published!.BookingId.Should().Be(domainEvent.BookingId.Value);
        published.UserId.Should().Be(domainEvent.UserId.Value);
        published.CatalogId.Should().Be(domainEvent.CatalogId.Value);
        published.Reason.Should().Be("changed my mind");
    }
}

public class PublishBookingConfirmationFailedHandlerTests
{
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();

    [Fact]
    public async Task Handle_PublishesIntegrationEventToBookingConfirmationFailedTopic()
    {
        var domainEvent = BookingConfirmationFailedEvent.Create(
            BookingId.New(),
            new UserId(Guid.NewGuid()),
            "booking already cancelled");

        BookingConfirmationFailedIntegrationEvent? published = null;
        await _publisher.PublishAsync(
            KafkaTopics.BookingConfirmationFailed,
            Arg.Do<BookingConfirmationFailedIntegrationEvent>(e => published = e),
            Arg.Any<CancellationToken>());

        await new PublishBookingConfirmationFailedHandler(_publisher).Handle(domainEvent, default);

        published.Should().NotBeNull();
        published!.BookingId.Should().Be(domainEvent.BookingId.Value);
        published.UserId.Should().Be(domainEvent.UserId.Value);
        published.Reason.Should().Be("booking already cancelled");
    }
}
