using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public static class DbContextOptionsExtensions
{
    /// <summary>Single place for provider setup so the API and the tests configure EF identically.</summary>
    public static DbContextOptionsBuilder UseSeatRushPostgres(this DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}
