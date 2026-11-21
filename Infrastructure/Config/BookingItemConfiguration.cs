using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Config;

public class BookingItemConfiguration : IEntityTypeConfiguration<BookingItem>
{
    public void Configure(EntityTypeBuilder<BookingItem> builder)
    {
        builder.Property(i => i.Price).HasPrecision(10, 2);

        builder.HasOne(i => i.Seat)
            .WithMany()
            .HasForeignKey(i => i.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        // Storage-level guarantee: a seat can appear in at most one booking, ever. Even if every other
        // check failed, Postgres would reject the second booking of the same seat.
        builder.HasIndex(i => i.SeatId).IsUnique().HasDatabaseName("ux_booking_items_one_booking_per_seat");
    }
}
