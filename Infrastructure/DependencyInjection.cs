using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        var redisConnection = config.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' is missing.");

        services.AddDbContext<SeatRushContext>(o => o.UseSeatRushPostgres(connectionString));

        // One multiplexer per process (it is thread-safe and pools internally).
        // abortConnect=false: the API still starts if Redis is down; Redis calls then fail and return 503.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnection);
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        services.AddSingleton(TimeProvider.System);

        services.Configure<BookingSessionOptions>(config.GetSection(BookingSessionOptions.SectionName));
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.SectionName));

        services.AddScoped<ISeatService, SeatService>();
        services.AddScoped<ISeatHoldService, SeatHoldService>();
        services.AddScoped<IBookingSessionService, BookingSessionService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddSingleton<IAuthTokenService, AuthTokenService>();

        return services;
    }
}
