using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Infrastructure.Services;

public class SeatHoldService(
    SeatRushContext db,
    IConnectionMultiplexer redis,
    IBookingSessionService sessions,
    ISeatMapNotifier notifier,
    TimeProvider clock) : ISeatHoldService
{
    private readonly IDatabase _redis = redis.GetDatabase();

    public async Task<HoldResult> HoldAsync(int seatId, Guid userId, CancellationToken ct = default)
    {
        // 1. The seat must exist and not be sold. Postgres is the authority on Booked.
        var seat = await db.Seats
            .AsNoTracking()
            .Where(s => s.Id == seatId)
            .Select(s => new { s.EventId, s.Status })
            .FirstOrDefaultAsync(ct);

        if (seat is null)
            return new HoldResult(HoldOutcome.SeatNotFound);
        if (seat.Status == SeatStatus.Booked)
            return new HoldResult(HoldOutcome.SeatUnavailable);

        // 2. Holds live exactly as long as the user's booking session.
        var session = await sessions.GetActiveAsync(seat.EventId, userId);
        var ttl = session is null ? TimeSpan.Zero : session.ExpiresAt - clock.GetUtcNow().UtcDateTime;
        if (session is null || ttl <= TimeSpan.Zero)
            return new HoldResult(HoldOutcome.NoActiveSession);

        var holdKey = RedisKeys.Hold(seat.EventId, seatId);
        var expiresAtMs = new DateTimeOffset(session.ExpiresAt).ToUnixTimeMilliseconds();
        await EnsureSeqAsync(seat.EventId);

        // 3. The atomic claim: a Redis transaction (WATCH/MULTI/EXEC) that only commits if nobody holds
        //    the seat. Redis runs commands one at a time, so of N simultaneous claims exactly one sees the
        //    key missing and commits; the others abort. All writes happen or none do, including the
        //    change counter, so every committed change gets a unique, ordered Seq.
        var tx = _redis.CreateTransaction();
        tx.AddCondition(Condition.KeyNotExists(holdKey));
        _ = tx.StringSetAsync(holdKey, userId.ToString("N"), ttl);
        _ = tx.SortedSetAddAsync(RedisKeys.HoldIndex(seat.EventId), seatId, expiresAtMs);
        var seq = tx.StringIncrementAsync(RedisKeys.Seq(seat.EventId));

        if (await tx.ExecuteAsync())
        {
            await notifier.SeatChangedAsync(new SeatChange(seat.EventId, seatId, SeatStatus.Held, session.ExpiresAt, await seq));
            return new HoldResult(HoldOutcome.Held, session.ExpiresAt);
        }

        // 4. Someone holds it. If it's this user, holding again is a no-op success (nothing changed, no push).
        var holder = await _redis.StringGetAsync(holdKey);
        return holder == userId.ToString("N")
            ? new HoldResult(HoldOutcome.Held, session.ExpiresAt)
            : new HoldResult(HoldOutcome.SeatUnavailable);
    }

    public async Task<bool> ReleaseAsync(int seatId, Guid userId, CancellationToken ct = default)
    {
        var eventId = await db.Seats
            .Where(s => s.Id == seatId)
            .Select(s => (int?)s.EventId)
            .FirstOrDefaultAsync(ct);

        if (eventId is null)
            return false;

        var holdKey = RedisKeys.Hold(eventId.Value, seatId);
        await EnsureSeqAsync(eventId.Value);

        // Only delete if the key still holds *this* user's id. A plain GET-then-DEL could delete someone
        // else's hold if ours expired and they grabbed the seat in between; the condition prevents that.
        var tx = _redis.CreateTransaction();
        tx.AddCondition(Condition.StringEqual(holdKey, userId.ToString("N")));
        _ = tx.KeyDeleteAsync(holdKey);
        _ = tx.SortedSetRemoveAsync(RedisKeys.HoldIndex(eventId.Value), seatId);
        var seq = tx.StringIncrementAsync(RedisKeys.Seq(eventId.Value));

        if (!await tx.ExecuteAsync())
            return false;

        await notifier.SeatChangedAsync(new SeatChange(eventId.Value, seatId, SeatStatus.Available, null, await seq));
        return true;
    }

    public async Task<IReadOnlyDictionary<int, ActiveHold>> GetActiveHoldsAsync(int eventId)
    {
        var index = RedisKeys.HoldIndex(eventId);
        var nowMs = clock.GetUtcNow().ToUnixTimeMilliseconds();

        // Drop index entries whose holds have expired (their keys are already gone via TTL).
        _ = _redis.SortedSetRemoveRangeByScoreAsync(index, double.NegativeInfinity, nowMs, flags: CommandFlags.FireAndForget);

        var entries = await _redis.SortedSetRangeByScoreWithScoresAsync(index, nowMs, double.PositiveInfinity);
        if (entries.Length == 0)
            return new Dictionary<int, ActiveHold>();

        // The hold keys are the truth (the index is only a lookup aid): read who holds each seat.
        var ids = entries.Select(e => (int)e.Element).ToArray();
        var holders = await _redis.StringGetAsync(ids.Select(id => (RedisKey)RedisKeys.Hold(eventId, id)).ToArray());

        var result = new Dictionary<int, ActiveHold>(ids.Length);
        for (var i = 0; i < ids.Length; i++)
        {
            if (holders[i].HasValue && Guid.TryParse(holders[i].ToString(), out var holder))
            {
                var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds((long)entries[i].Score).UtcDateTime;
                result[ids[i]] = new ActiveHold(holder, expiresAt);
            }
        }

        return result;
    }

    public async Task<long> GetSeqAsync(int eventId)
    {
        await EnsureSeqAsync(eventId);
        return (long)await _redis.StringGetAsync(RedisKeys.Seq(eventId));
    }

    public async Task<bool> AreAllHeldByAsync(int eventId, IReadOnlyCollection<int> seatIds, Guid userId)
    {
        var holders = await _redis.StringGetAsync(seatIds.Select(id => (RedisKey)RedisKeys.Hold(eventId, id)).ToArray());
        var me = userId.ToString("N");
        return holders.All(h => h == me);
    }

    public async Task MarkBookedAsync(int eventId, IReadOnlyCollection<int> seatIds)
    {
        await EnsureSeqAsync(eventId);

        // The seats are Booked in Postgres now, which already wins over any hold. Removing the holds and
        // bumping the counter is cleanup + live notification, so no condition is needed here.
        var tx = _redis.CreateTransaction();
        foreach (var id in seatIds)
            _ = tx.KeyDeleteAsync(RedisKeys.Hold(eventId, id));
        _ = tx.SortedSetRemoveAsync(RedisKeys.HoldIndex(eventId), seatIds.Select(id => (RedisValue)id).ToArray());
        var seqs = seatIds.Select(id => (SeatId: id, Seq: tx.StringIncrementAsync(RedisKeys.Seq(eventId)))).ToList();
        await tx.ExecuteAsync();

        foreach (var (seatId, seq) in seqs)
            await notifier.SeatChangedAsync(new SeatChange(eventId, seatId, SeatStatus.Booked, null, await seq));
    }

    /// <summary>
    /// Starts the counter at (now in ms × 1000) instead of 0. If Redis ever loses its data, the counter
    /// restarts from a *later* time, so it still only moves forward and clients never mistake new changes for old ones.
    /// </summary>
    private Task EnsureSeqAsync(int eventId) =>
        _redis.StringSetAsync(RedisKeys.Seq(eventId), clock.GetUtcNow().ToUnixTimeMilliseconds() * 1000, when: When.NotExists);
}
