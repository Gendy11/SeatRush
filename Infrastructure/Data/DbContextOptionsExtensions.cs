using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public static class DbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UseSeatRushPostgres(this DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}
