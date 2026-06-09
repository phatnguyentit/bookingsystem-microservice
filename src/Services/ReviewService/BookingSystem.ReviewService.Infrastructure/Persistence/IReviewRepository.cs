namespace BookingSystem.ReviewService.Infrastructure.Persistence;

public interface IReviewRepository
{
    Task AddAsync(Review review, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Review>> GetByListingAsync(Guid catalogId, CancellationToken cancellationToken = default);
}
