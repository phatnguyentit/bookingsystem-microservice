using BookingSystem.BookingService.Domain.ValueObjects;
using FluentAssertions;

namespace BookingService.Domain.Tests.ValueObjects;

public class BookingIdTests
{
    [Fact]
    public void New_GeneratesNonEmptyValue()
    {
        BookingId.New().Value.Should().NotBeEmpty();
    }

    [Fact]
    public void New_CalledTwice_GeneratesDistinctIds()
    {
        BookingId.New().Should().NotBe(BookingId.New());
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var value = Guid.NewGuid();

        new BookingId(value).Should().Be(new BookingId(value));
    }

    [Fact]
    public void ToString_ReturnsUnderlyingGuidString()
    {
        var value = Guid.NewGuid();

        new BookingId(value).ToString().Should().Be(value.ToString());
    }
}

public class UserIdTests
{
    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var value = Guid.NewGuid();

        new UserId(value).Should().Be(new UserId(value));
    }

    [Fact]
    public void ToString_ReturnsUnderlyingGuidString()
    {
        var value = Guid.NewGuid();

        new UserId(value).ToString().Should().Be(value.ToString());
    }
}

public class CatalogIdTests
{
    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var value = Guid.NewGuid();

        new CatalogId(value).Should().Be(new CatalogId(value));
    }

    [Fact]
    public void ToString_ReturnsUnderlyingGuidString()
    {
        var value = Guid.NewGuid();

        new CatalogId(value).ToString().Should().Be(value.ToString());
    }
}
