using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Config;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);

        builder.Property(s => s.Version).IsConcurrencyToken();

        builder.HasOne(s => s.Event)
            .WithMany(e => e.Seats)
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.VenueSeat)
            .WithMany()
            .HasForeignKey(s => s.VenueSeatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.EventId, s.VenueSeatId }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("ck_seats_status", "status IN ('Available', 'Booked')"));
    }
}
