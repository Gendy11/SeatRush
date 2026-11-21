using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Config;

public class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> builder)
    {
        builder.Property(m => m.Title).HasMaxLength(200);
        builder.Property(m => m.PosterUrl).HasMaxLength(500);
        builder.Property(m => m.BackdropUrl).HasMaxLength(500);
        builder.Property(m => m.AgeRating).HasMaxLength(8);

        builder.HasIndex(m => m.Title).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("ck_movies_duration_positive", "duration_minutes > 0"));
    }
}
