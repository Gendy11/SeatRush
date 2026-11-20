using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Config;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.Property(b => b.IdempotencyKey).HasMaxLength(100);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(16);

        builder.HasOne(b => b.Seat)
            .WithMany()
            .HasForeignKey(b => b.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.IdempotencyKey).IsUnique();

        // Storage-level guarantee: a seat can have at most one confirmed booking, ever.
        builder.HasIndex(b => b.SeatId)
            .IsUnique()
            .HasFilter("status = 'Confirmed'")
            .HasDatabaseName("ux_bookings_one_confirmed_per_seat");
    }
}
