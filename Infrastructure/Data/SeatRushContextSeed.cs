using System.Text.Json;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public static class SeatRushContextSeed
{
    private record VenueSeed(string Name, int Rows, int SeatsPerRow, decimal TicketPrice);

    // Show times are fixed UTC dates far enough ahead that the seeded showings stay upcoming.
    private record EventSeed(string Movie, string Venue, DateTime ShowTime);

    private record MovieSeed(string Title, string PosterUrl, string BackdropUrl, string AgeRating, int DurationMinutes);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task SeedAsync(SeatRushContext context, CancellationToken ct = default)
    {
        await SeedVenuesAsync(context, ct);
        await SeedMoviesAsync(context, ct);
        await SeedEventsAsync(context, ct);
    }

    // Each seeder only ADDS what's missing (matched by natural key), so adding a venue, movie or showing to the
    // JSON files later shows up on the next start without wiping the database or anyone's bookings.

    /// <summary>Adds movies from SeedData/movies.json that don't exist yet (by title).</summary>
    private static async Task SeedMoviesAsync(SeatRushContext context, CancellationToken ct)
    {
        var existing = (await context.Movies.Select(m => m.Title).ToListAsync(ct)).ToHashSet();
        var seeds = await ReadSeedFileAsync<MovieSeed>("movies.json", ct);

        context.Movies.AddRange(seeds
            .Where(s => !existing.Contains(s.Title))
            .Select(s => new Movie
            {
                Title = s.Title,
                PosterUrl = s.PosterUrl,
                BackdropUrl = s.BackdropUrl,
                AgeRating = s.AgeRating,
                DurationMinutes = s.DurationMinutes
            }));
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Adds venues (with their seat layouts) from SeedData/venues.json that don't exist yet (by name).</summary>
    private static async Task SeedVenuesAsync(SeatRushContext context, CancellationToken ct)
    {
        var existing = (await context.Venues.Select(v => v.Name).ToListAsync(ct)).ToHashSet();
        var seeds = await ReadSeedFileAsync<VenueSeed>("venues.json", ct);

        context.Venues.AddRange(seeds
            .Where(s => !existing.Contains(s.Name))
            .Select(s => Venue.WithLayout(s.Name, s.Rows, s.SeatsPerRow, s.TicketPrice)));
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Adds showings from SeedData/events.json that don't exist yet (by venue + show time).
    /// Each new showing gets its own seat inventory.
    /// </summary>
    private static async Task SeedEventsAsync(SeatRushContext context, CancellationToken ct)
    {
        var seeds = await ReadSeedFileAsync<EventSeed>("events.json", ct);

        var existing = (await context.Events
                .Select(e => new { Venue = e.Venue!.Name, e.ShowTime })
                .ToListAsync(ct))
            .Select(e => (e.Venue, e.ShowTime))
            .ToHashSet();

        var movies = await context.Movies.ToDictionaryAsync(m => m.Title, ct);
        var venues = new Dictionary<string, Venue>();
        var scheduled = new HashSet<(string Venue, DateTime ShowTime)>();

        foreach (var s in seeds)
        {
            var showTime = s.ShowTime.ToUniversalTime();

            // FR3: one showing per venue per show time. The unique index (venue_id, show_time) enforces this
            // in the database; this check just fails early with a readable message.
            if (!scheduled.Add((s.Venue, showTime)))
                throw new InvalidOperationException($"events.json has two showings in '{s.Venue}' at {showTime:u}.");

            if (existing.Contains((s.Venue, showTime)))
                continue;

            if (!movies.TryGetValue(s.Movie, out var movie))
                throw new InvalidOperationException($"events.json references unknown movie '{s.Movie}'.");

            // Venue seats must be loaded: Event.For copies them into per-event Seat rows.
            if (!venues.TryGetValue(s.Venue, out var venue))
            {
                venue = await context.Venues.Include(v => v.Seats).FirstOrDefaultAsync(v => v.Name == s.Venue, ct)
                        ?? throw new InvalidOperationException($"events.json references unknown venue '{s.Venue}'.");
                venues[s.Venue] = venue;
            }

            context.Events.Add(Event.For(movie, venue, showTime));
        }

        await context.SaveChangesAsync(ct);
    }

    private static async Task<List<T>> ReadSeedFileAsync<T>(string fileName, CancellationToken ct)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", fileName);
        await using var file = File.OpenRead(path);

        return await JsonSerializer.DeserializeAsync<List<T>>(file, JsonOptions, ct)
               ?? throw new InvalidOperationException($"{fileName} is empty.");
    }
}
