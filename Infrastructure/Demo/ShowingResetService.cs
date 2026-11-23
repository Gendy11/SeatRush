using Core.Interfaces;
using Core.Models;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Infrastructure.Demo;

public class ShowingResetService(
    SeatRushContext db,
    IConnectionMultiplexer redis,
    IRushSimulator rush,
    ISeatMapNotifier notifier) : IShowingResetService
{
    public async Task<bool> ResetAsync(int eventId, CancellationToken ct = default)
    {
        if (!await db.Events.AnyAsync(e => e.Id == eventId, ct))
            return false;

        await rush.StopAsync(eventId);

        var botIds = $"{RushBots.IdPrefix}%";
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE seats SET status = 'Available', version = version + 1
                WHERE id IN (
                    SELECT bi.seat_id FROM booking_items bi
                    JOIN bookings b ON b.id = bi.booking_id
                    WHERE b.event_id = {eventId} AND b.user_id::text LIKE {botIds})
                """, ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM bookings WHERE event_id = {eventId} AND user_id::text LIKE {botIds}", ct);

            await tx.CommitAsync(ct);
        }

        var keep = (RedisKey)RedisKeys.Seq(eventId);
        var database = redis.GetDatabase();
        foreach (var server in redis.GetServers().Where(s => s.IsConnected && !s.IsReplica))
        {
            var keys = new List<RedisKey>();
            await foreach (var key in server.KeysAsync(pattern: $"*{{e:{eventId}}}*"))
                if (key != keep)
                    keys.Add(key);

            if (keys.Count > 0)
                await database.KeyDeleteAsync(keys.ToArray());
        }

        await database.SetRemoveAsync(RedisKeys.ActiveQueues, eventId);

        await notifier.ShowingResetAsync(eventId);
        return true;
    }
}
