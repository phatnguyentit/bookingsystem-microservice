using Microsoft.EntityFrameworkCore;

namespace BookingSystem.CatalogService.Infrastructure.Persistence;

public class Catalog
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Catalog> Catalogs => Set<Catalog>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Catalog>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Title).HasMaxLength(300).IsRequired();
            e.Property(l => l.PricePerNight).HasColumnType("decimal(18,2)");
            e.Property(l => l.Currency).HasMaxLength(3);
            e.ToTable("catalogs");
        });
    }
}
