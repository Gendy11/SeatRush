using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Config;

public class VenueSeatConfiguration : IEntityTypeConfiguration<VenueSeat>
{
    public void Configure(EntityTypeBuilder<VenueSeat> builder)
    {
        builder.Property(s => s.Row).HasMaxLength(4);

        builder.HasOne(s => s.Venue)
            .WithMany(v => v.Seats)
            .HasForeignKey(s => s.VenueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.VenueId, s.Row, s.Number }).IsUnique();
    }
}
