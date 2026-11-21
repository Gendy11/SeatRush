using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Config;

public class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.Property(v => v.Name).HasMaxLength(100);
        builder.Property(v => v.TicketPrice).HasPrecision(10, 2);
        builder.HasIndex(v => v.Name).IsUnique();
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_venues_capacity_positive", "capacity > 0");
            t.HasCheckConstraint("ck_venues_ticket_price_positive", "ticket_price > 0");
        });
    }
}
