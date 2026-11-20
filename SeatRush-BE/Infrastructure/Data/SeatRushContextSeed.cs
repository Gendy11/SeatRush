using System.Text.Json;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public static class SeatRushContextSeed
{
    private record VenueSeed(string Name, int Rows, int SeatsPerRow);

    // Show times are relative to today so the seeded showings are always in the future.
    private record EventSeed(string Name, string Venue, int DaysFromNow, TimeOnly Time);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task SeedAsync(SeatRushContext context, CancellationToken ct = default)
    {
        await SeedVenuesAsync(context, ct);
        await SeedEventsAsync(context, ct);
    }

    /// <summary>Seeds the venues (with their seat layouts) from SeedData/venues.json if none exist.</summary>
    private static async Task SeedVenuesAsync(SeatRushContext context, CancellationToken ct)
    {
        if (await context.Venues.AnyAsync(ct))
            return;

        var seeds = await ReadSeedFileAsync<VenueSeed>("venues.json", ct);

        context.Venues.AddRange(seeds.Select(s => Venue.WithLayout(s.Name, s.Rows, s.SeatsPerRow)));
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Seeds showings from SeedData/events.json if none exist. Each gets its own seat inventory.</summary>
    private static async Task SeedEventsAsync(SeatRushContext context, CancellationToken ct)
    {
        if (await context.Events.AnyAsync(ct))
            return;

        var seeds = await ReadSeedFileAsync<EventSeed>("events.json", ct);

        // Venue seats must be loaded: Event.For copies them into per-event Seat rows.
        var venues = await context.Venues.Include(v => v.Seats).ToDictionaryAsync(v => v.Name, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var scheduled = new HashSet<(string Venue, DateTime ShowTime)>();

        foreach (var s in seeds)
        {
            if (!venues.TryGetValue(s.Venue, out var venue))
                throw new InvalidOperationException($"events.json references unknown venue '{s.Venue}'.");

            var showTime = today.AddDays(s.DaysFromNow).ToDateTime(s.Time, DateTimeKind.Utc);

            // FR3: one showing per venue per show time. The unique index (venue_id, show_time) enforces this
            // in the database; this check just fails early with a readable message.
            if (!scheduled.Add((s.Venue, showTime)))
                throw new InvalidOperationException($"events.json has two showings in '{s.Venue}' at {showTime:u}.");

            context.Events.Add(Event.For(venue, s.Name, showTime));
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
