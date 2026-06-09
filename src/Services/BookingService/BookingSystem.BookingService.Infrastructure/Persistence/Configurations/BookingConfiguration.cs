using BookingSystem.BookingService.Domain;
using BookingSystem.BookingService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingSystem.BookingService.Infrastructure.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasConversion(id => id.Value, v => new BookingId(v));

        builder.Property(b => b.UserId)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(b => b.CatalogId)
            .HasConversion(id => id.Value, v => new CatalogId(v));

        builder.OwnsOne(b => b.Period, p =>
        {
            p.Property(x => x.CheckIn).HasColumnName("check_in");
            p.Property(x => x.CheckOut).HasColumnName("check_out");
            p.HasIndex(x => x.CheckIn).HasDatabaseName("IX_bookings_check_in");
            p.HasIndex(x => x.CheckOut).HasDatabaseName("IX_bookings_check_out");
        });

        builder.OwnsOne(b => b.TotalPrice, m =>
        {
            m.Property(x => x.Amount).HasColumnName("price_amount")
                .HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("price_currency")
                .HasMaxLength(3);
        });

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(b => b.Status).HasDatabaseName("IX_bookings_status");

        builder.ToTable("bookings");
    }
}
