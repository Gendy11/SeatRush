using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Config;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        // Stored as text so raw SQL (status = 'Held') stays readable.
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

        // Each venue seat is sold at most once per event.
        builder.HasIndex(s => new { s.EventId, s.VenueSeatId }).IsUnique();

        // Supports the per-user hold-limit count.
        builder.HasIndex(s => new { s.HeldBy, s.Status });

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_seats_held_has_owner_and_expiry",
            "status <> 'Held' OR (held_by IS NOT NULL AND hold_expires_at IS NOT NULL)"));
    }
}
