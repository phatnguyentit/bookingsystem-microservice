using BookingSystem.ReviewService.Api.Features.CreateReview;
using BookingSystem.ReviewService.Infrastructure.Persistence;
using FluentAssertions;
using NSubstitute;

namespace ReviewService.Tests.Features;

public class CreateReviewHandlerTests
{
    private readonly IReviewRepository _repo = Substitute.For<IReviewRepository>();

    private static CreateReviewCommand CreateCommand(int rating) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), rating, "Great stay!");

    [Fact]
    public async Task Handle_ValidCommand_PersistsReviewAndReturnsId()
    {
        Review? added = null;
        await _repo.AddAsync(Arg.Do<Review>(r => added = r), Arg.Any<CancellationToken>());
        var cmd = CreateCommand(rating: 4);

        var result = await new CreateReviewHandler(_repo).Handle(cmd, default);

        added.Should().NotBeNull();
        added!.BookingId.Should().Be(cmd.BookingId);
        added.CatalogId.Should().Be(cmd.CatalogId);
        added.UserId.Should().Be(cmd.UserId);
        added.Rating.Should().Be(4);
        added.Comment.Should().Be("Great stay!");
        result.Should().Be(added.Id);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task Handle_BoundaryRatings_AreAccepted(int rating)
    {
        var act = () => new CreateReviewHandler(_repo).Handle(CreateCommand(rating), default);

        await act.Should().NotThrowAsync();
        await _repo.Received(1).AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Handle_RatingOutOfRange_ThrowsWithoutPersisting(int rating)
    {
        var act = () => new CreateReviewHandler(_repo).Handle(CreateCommand(rating), default);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await _repo.DidNotReceive().AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }
}
