using Microsoft.EntityFrameworkCore;

namespace BookingSystem.ReviewService.Infrastructure.Persistence;

public class Review
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid CatalogId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ReviewDbContext(DbContextOptions<ReviewDbContext> options) : DbContext(options)
{
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Review>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.BookingId).HasColumnName("booking_id");
            e.Property(r => r.CatalogId).HasColumnName("catalog_id");
            e.Property(r => r.UserId).HasColumnName("user_id");
            e.Property(r => r.Rating).HasColumnName("rating");
            e.Property(r => r.Comment).HasColumnName("comment").HasMaxLength(2000);
            e.Property(r => r.CreatedAt).HasColumnName("created_at");
            e.ToTable("reviews");
        });
    }
}