using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<SeatRushContext>(o => o.UseSeatRushPostgres(connectionString));
        services.Configure<SeatHoldOptions>(config.GetSection(SeatHoldOptions.SectionName));
        services.AddScoped<ISeatService, SeatService>();

        return services;
    }
}
