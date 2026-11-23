using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Services;

public class WaitingRoomService(
    IConnectionMultiplexer redis,
    IOptions<WaitingRoomOptions> options,
    TimeProvider clock) : IWaitingRoomService
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly WaitingRoomOptions _options = options.Value;

    public async Task<QueueStatus> JoinAsync(int eventId, Guid userId)
    {
        var user = Member(userId);

        if (await GetAdmittedUntilAsync(eventId, user) is not null)
            return await GetStatusAsync(eventId, userId);

        if (await _db.SortedSetScoreAsync(RedisKeys.Queue(eventId), user) is null
            && !await TryRestoreSkippedAsync(eventId, user))
        {
            var ticket = await _db.StringIncrementAsync(RedisKeys.QueueTicket(eventId));
            await _db.SortedSetAddAsync(RedisKeys.Queue(eventId), user, ticket, SortedSetWhen.NotExists);
        }

        await MarkSeenAsync(eventId, user);
        await _db.SetAddAsync(RedisKeys.ActiveQueues, eventId);

        await AdmitAsync(eventId);

        return await GetStatusAsync(eventId, userId);
    }

    public async Task<QueueStatus> GetStatusAsync(int eventId, Guid userId)
    {
        var user = Member(userId);

        if (await GetAdmittedUntilAsync(eventId, user) is { } until)
            return new QueueStatus(QueueState.Admitted, AdmittedUntil: until);

        var rank = await _db.SortedSetRankAsync(RedisKeys.Queue(eventId), user);

        if (rank is null && await TryRestoreSkippedAsync(eventId, user))
            rank = await _db.SortedSetRankAsync(RedisKeys.Queue(eventId), user);

        if (rank is null)
            return new QueueStatus(QueueState.NotQueued);

        await MarkSeenAsync(eventId, user);
        return new QueueStatus(QueueState.Waiting, Position: rank + 1);
    }

    public async Task<bool> IsAdmittedAsync(int eventId, Guid userId) =>
        await GetAdmittedUntilAsync(eventId, Member(userId)) is not null;

    public async Task EnterAsync(int eventId, Guid userId, DateTime sessionExpiresAt)
    {
        var user = Member(userId);
        await _db.SortedSetAddAsync(RedisKeys.Room(eventId), user, ToUnixMs(sessionExpiresAt));
        await _db.HashDeleteAsync(RedisKeys.RoomPass(eventId), user);
    }

    public Task LeaveAsync(int eventId, Guid userId) =>
        _db.SortedSetRemoveAsync(RedisKeys.Room(eventId), Member(userId));

    public async Task LeaveLineAsync(int eventId, Guid userId)
    {
        var user = Member(userId);
        await _db.SortedSetRemoveAsync(RedisKeys.Queue(eventId), user);
        await _db.SortedSetRemoveAsync(RedisKeys.QueueSkipped(eventId), user);
        await _db.HashDeleteAsync(RedisKeys.QueueSeen(eventId), user);
    }

    public async Task<int> AdmitAsync(int eventId)
    {
        // one admitter at a time across instances
        var lockKey = RedisKeys.RoomLock(eventId);
        var lockToken = Guid.NewGuid().ToString("N");
        if (!await _db.StringSetAsync(lockKey, lockToken, TimeSpan.FromSeconds(5), When.NotExists))
            return 0;

        try
        {
            var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
            var room = RedisKeys.Room(eventId);
            var queue = RedisKeys.Queue(eventId);

            await ClearExpiredAsync(eventId, now);
            var free = _options.Capacity - (int)await _db.SortedSetLengthAsync(room);

            var admitted = 0;
            var staleBefore = now - (long)_options.StaleAfter.TotalMilliseconds;
            var passUntil = now + (long)_options.PassDuration.TotalMilliseconds;

            while (free > 0)
            {
                var next = await _db.SortedSetPopAsync(queue, free);
                if (next.Length == 0)
                    break;

                foreach (var entry in next)
                {
                    var seen = await _db.HashGetAsync(RedisKeys.QueueSeen(eventId), entry.Element);
                    await _db.HashDeleteAsync(RedisKeys.QueueSeen(eventId), entry.Element);

                    if (!seen.HasValue || (long)seen < staleBefore)
                    {
                        await _db.SortedSetAddAsync(RedisKeys.QueueSkipped(eventId), entry.Element, entry.Score);
                        continue;
                    }

                    await _db.SortedSetAddAsync(room, entry.Element, passUntil);
                    await _db.HashSetAsync(RedisKeys.RoomPass(eventId), entry.Element, entry.Score);
                    admitted++;
                    free--;
                }
            }

            if (await _db.SortedSetLengthAsync(queue) == 0)
            {
                await _db.SetRemoveAsync(RedisKeys.ActiveQueues, eventId);
                if (await _db.SortedSetLengthAsync(queue) > 0)
                    await _db.SetAddAsync(RedisKeys.ActiveQueues, eventId);
            }

            return admitted;
        }
        finally
        {
            var release = _db.CreateTransaction();
            release.AddCondition(Condition.StringEqual(lockKey, lockToken));
            _ = release.KeyDeleteAsync(lockKey);
            await release.ExecuteAsync();
        }
    }

    public async Task AdmitAllAsync()
    {
        foreach (var id in await _db.SetMembersAsync(RedisKeys.ActiveQueues))
            if (id.TryParse(out int eventId))
                await AdmitAsync(eventId);
    }

    private async Task<DateTime?> GetAdmittedUntilAsync(int eventId, RedisValue user)
    {
        var deadline = await _db.SortedSetScoreAsync(RedisKeys.Room(eventId), user);
        if (deadline is null || deadline <= clock.GetUtcNow().ToUnixTimeMilliseconds())
            return null;

        return DateTimeOffset.FromUnixTimeMilliseconds((long)deadline).UtcDateTime;
    }

    private async Task ClearExpiredAsync(int eventId, long now)
    {
        var room = RedisKeys.Room(eventId);
        var expired = await _db.SortedSetRangeByScoreAsync(room, double.NegativeInfinity, now);
        if (expired.Length == 0)
            return;

        var passes = RedisKeys.RoomPass(eventId);
        var tickets = await _db.HashGetAsync(passes, expired);

        for (var i = 0; i < expired.Length; i++)
        {
            if (tickets[i].HasValue)
                await _db.SortedSetAddAsync(RedisKeys.QueueSkipped(eventId), expired[i], (double)tickets[i]);
        }

        await _db.HashDeleteAsync(passes, expired);
        await _db.SortedSetRemoveAsync(room, expired);
    }

    private async Task<bool> TryRestoreSkippedAsync(int eventId, RedisValue user)
    {
        var skipped = RedisKeys.QueueSkipped(eventId);
        var ticket = await _db.SortedSetScoreAsync(skipped, user);
        if (ticket is null)
            return false;

        var restore = _db.CreateTransaction();
        restore.AddCondition(Condition.SortedSetContains(skipped, user));
        _ = restore.SortedSetAddAsync(RedisKeys.Queue(eventId), user, ticket.Value);
        _ = restore.SortedSetRemoveAsync(skipped, user);
        _ = restore.HashSetAsync(RedisKeys.QueueSeen(eventId), user, clock.GetUtcNow().ToUnixTimeMilliseconds());

        await restore.ExecuteAsync();
        await _db.SetAddAsync(RedisKeys.ActiveQueues, eventId);
        return true;
    }

    private Task MarkSeenAsync(int eventId, RedisValue user) =>
        _db.HashSetAsync(RedisKeys.QueueSeen(eventId), user, clock.GetUtcNow().ToUnixTimeMilliseconds());

    private static RedisValue Member(Guid userId) => userId.ToString("N");

    private static long ToUnixMs(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}
