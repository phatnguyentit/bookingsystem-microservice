using BookingSystem.ReviewService.Infrastructure.Persistence;
using MediatR;

namespace BookingSystem.ReviewService.Api.Features.CreateReview;

public record CreateReviewCommand(
    Guid BookingId,
    Guid CatalogId,
    Guid UserId,
    int Rating,
    string Comment) : IRequest<Guid>;

public class CreateReviewHandler(IReviewRepository repo)
    : IRequestHandler<CreateReviewCommand, Guid>
{
    public async Task<Guid> Handle(CreateReviewCommand cmd, CancellationToken cancellationToken)
    {
        if (cmd.Rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(cmd.Rating), "Rating must be between 1 and 5.");

        var review = new Review
        {
            Id = Guid.NewGuid(),
            BookingId = cmd.BookingId,
            CatalogId = cmd.CatalogId,
            UserId = cmd.UserId,
            Rating = cmd.Rating,
            Comment = cmd.Comment,
            CreatedAt = DateTime.UtcNow
        };

        await repo.AddAsync(review, cancellationToken);
        return review.Id;
    }
}
