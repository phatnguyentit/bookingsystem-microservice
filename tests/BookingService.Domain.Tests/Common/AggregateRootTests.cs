using BookingSystem.BookingService.Domain.Common;
using FluentAssertions;

namespace BookingService.Domain.Tests.Common;

public class AggregateRootTests
{
    private record StubEvent(int Number) : IDomainEvent;

    private class StubAggregate : AggregateRoot<Guid>
    {
        public StubAggregate() => Id = Guid.NewGuid();
        public void Raise(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);
    }

    [Fact]
    public void DomainEvents_NewAggregate_IsEmpty()
    {
        new StubAggregate().DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddDomainEvent_MultipleEvents_AccumulatesInOrder()
    {
        var aggregate = new StubAggregate();

        aggregate.Raise(new StubEvent(1));
        aggregate.Raise(new StubEvent(2));

        aggregate.DomainEvents.Should().Equal(new StubEvent(1), new StubEvent(2));
    }

    [Fact]
    public void ClearDomainEvents_WithPendingEvents_EmptiesCollection()
    {
        var aggregate = new StubAggregate();
        aggregate.Raise(new StubEvent(1));

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Id_SetInConstructor_IsExposedByGenericBase()
    {
        var aggregate = new StubAggregate();

        aggregate.Id.Should().NotBeEmpty();
    }
}
