using Core.Entities;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Config;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.Property(b => b.Reference).HasMaxLength(12);
        builder.Property(b => b.IdempotencyKey).HasMaxLength(100);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.PaymentMethod).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.TotalAmount).HasPrecision(10, 2);

        builder.HasOne(b => b.Event)
            .WithMany()
            .HasForeignKey(b => b.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Items)
            .WithOne(i => i.Booking)
            .HasForeignKey(i => i.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.Reference).IsUnique();

        builder.HasIndex(b => new { b.UserId, b.IdempotencyKey }).IsUnique();

        builder.HasIndex(b => new { b.UserId, b.CreatedAt });
    }
}
