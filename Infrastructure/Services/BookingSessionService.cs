using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Services;

public class BookingSessionService(
    IConnectionMultiplexer redis,
    IOptions<BookingSessionOptions> options,
    TimeProvider clock) : IBookingSessionService
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly TimeSpan _duration = options.Value.Duration;

    public async Task<BookingSession> GetOrStartAsync(int eventId, Guid userId)
    {
        var key = RedisKeys.Session(eventId, userId);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var expiresAt = clock.GetUtcNow().Add(_duration);

            var created = await _db.StringSetAsync(key, expiresAt.ToUnixTimeMilliseconds(), _duration, When.NotExists);
            if (created)
                return new BookingSession(eventId, userId, expiresAt.UtcDateTime);

            var existing = await GetActiveAsync(eventId, userId);
            if (existing is not null)
                return existing;
        }

        throw new InvalidOperationException("Could not start a booking session.");
    }

    public async Task EndAsync(int eventId, Guid userId)
    {
        await _db.KeyDeleteAsync(RedisKeys.Session(eventId, userId));

        await _db.SortedSetRemoveAsync(RedisKeys.Room(eventId), userId.ToString("N"));
    }

    public async Task<BookingSession?> GetActiveAsync(int eventId, Guid userId)
    {
        var value = await _db.StringGetAsync(RedisKeys.Session(eventId, userId));
        if (!value.HasValue || !long.TryParse(value.ToString(), out var unixMs))
            return null;

        var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
        return expiresAt > clock.GetUtcNow()
            ? new BookingSession(eventId, userId, expiresAt.UtcDateTime)
            : null;
    }
}
