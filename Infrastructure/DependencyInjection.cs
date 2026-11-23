using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Demo;
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

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnection);
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        services.AddSingleton(TimeProvider.System);

        services.Configure<BookingSessionOptions>(config.GetSection(BookingSessionOptions.SectionName));
        services.Configure<WaitingRoomOptions>(config.GetSection(WaitingRoomOptions.SectionName));
        services.Configure<RushOptions>(config.GetSection(RushOptions.SectionName));
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.SectionName));

        services.AddScoped<ISeatService, SeatService>();
        services.AddScoped<ISeatHoldService, SeatHoldService>();
        services.AddScoped<IBookingSessionService, BookingSessionService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddSingleton<IAuthTokenService, AuthTokenService>();
        services.AddSingleton<IWaitingRoomService, WaitingRoomService>();

        services.AddSingleton<IRushSimulator, RushSimulator>();
        services.AddScoped<IShowingResetService, ShowingResetService>();

        return services;
    }
}
