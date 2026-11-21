using Core.Entities;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Real Postgres + Redis in throwaway Docker containers, shared by all tests in the collection.
/// Each test creates its own showing, so tests don't interfere with each other.
/// </summary>
public class SeatRushFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public IConnectionMultiplexer Redis { get; private set; } = null!;

    /// <summary>Every change pushed by any hold service created by this fixture.</summary>
    public RecordingNotifier Notifier { get; } = new();

    private string ConnectionString =>
        // Keep the pool well below Postgres' max_connections so heavy concurrent tests queue instead of failing.
        _postgres.GetConnectionString() + ";Maximum Pool Size=40";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        await using var db = NewDbContext();
        await db.Database.MigrateAsync();

        Redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await Redis.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }

    public SeatRushContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<SeatRushContext>();
        options.UseSeatRushPostgres(ConnectionString);
        return new SeatRushContext(options.Options);
    }

    public BookingSessionService NewSessionService(TimeSpan? duration = null) =>
        new(Redis, Options.Create(new BookingSessionOptions { Duration = duration ?? TimeSpan.FromMinutes(7) }), TimeProvider.System);

    /// <summary>A hold service with its own DbContext, like one HTTP request would get.</summary>
    public SeatHoldService NewHoldService(BookingSessionService? sessions = null) =>
        new(NewDbContext(), Redis, sessions ?? NewSessionService(), Notifier, TimeProvider.System);

    public SeatService NewSeatService() => new(NewDbContext(), NewHoldService());

    public BookingService NewBookingService() =>
        new(NewDbContext(), NewHoldService(), NewSessionService(), TimeProvider.System, NullLogger<BookingService>.Instance);

    /// <summary>Starts a session for the user and holds the given seats.</summary>
    public async Task HoldSeatsAsync(int eventId, Guid userId, params int[] seatIds)
    {
        await NewSessionService().GetOrStartAsync(eventId, userId);
        foreach (var id in seatIds)
            await NewHoldService().HoldAsync(id, userId);
    }

    /// <summary>Creates a fresh showing (own venue + movie) and returns its id and seat ids.</summary>
    public async Task<(int EventId, int[] SeatIds)> CreateShowingAsync(int rows = 2, int seatsPerRow = 5)
    {
        await using var db = NewDbContext();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var venue = Venue.WithLayout($"Venue {suffix}", rows, seatsPerRow, ticketPrice: 200m);
        var movie = new Movie { Title = $"Movie {suffix}", PosterUrl = "posters/x.jpg", BackdropUrl = "backdrops/x.jpg", AgeRating = "PG", DurationMinutes = 100 };
        var ev = Event.For(movie, venue, DateTime.UtcNow.AddDays(1));

        db.Events.Add(ev);
        await db.SaveChangesAsync();

        return (ev.Id, ev.Seats.Select(s => s.Id).OrderBy(id => id).ToArray());
    }
}

[CollectionDefinition(Name)]
public class SeatRushCollection : ICollectionFixture<SeatRushFixture>
{
    public const string Name = "SeatRush";
}
