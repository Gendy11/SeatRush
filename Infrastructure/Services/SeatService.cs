using Core.Entities;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public class SeatService(SeatRushContext db, IOptions<SeatHoldOptions> options) : ISeatService
{
    // Namespace for pg_advisory_xact_lock(int, int) so hold locks never collide with other advisory locks.
    private const int HoldLockNamespace = 1;

    private readonly SeatHoldOptions _options = options.Value;

    public async Task<HoldResult> HoldSeatAsync(int seatId, Guid userId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // 1. Serialize hold attempts *by the same user* so the per-user limit below can't be raced
        //    (two parallel requests both seeing "3 held"). Different users never wait on each other here;
        //    the lock is released automatically when the transaction ends.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({HoldLockNamespace}, hashtext({userId.ToString()}))", ct);

        // 2. The atomic claim. Postgres row-locks the seat; a concurrent transaction targeting the same seat
        //    waits, then re-checks the WHERE clause against the committed row and updates 0 rows.
        //    An expired hold counts as available (lazy expiry), so correctness never depends on the sweeper.
        var expiresAt = await db.Database.SqlQuery<DateTime>($"""
            UPDATE seats AS s
            SET status = 'Held',
                held_by = {userId},
                hold_expires_at = now() + {_options.HoldDuration},
                version = s.version + 1
            WHERE s.id = {seatId}
              AND (s.status = 'Available' OR (s.status = 'Held' AND s.hold_expires_at < now()))
              AND (SELECT count(*)
                   FROM seats AS h
                   WHERE h.event_id = s.event_id
                     AND h.held_by = {userId}
                     AND h.status = 'Held'
                     AND h.hold_expires_at >= now()) < {_options.MaxHeldSeatsPerUser}
            RETURNING s.hold_expires_at AS "Value"
            """).ToListAsync(ct);

        if (expiresAt.Count == 1)
        {
            await tx.CommitAsync(ct);
            return new HoldResult(HoldOutcome.Held, expiresAt[0]);
        }

        // 3. Claim failed. Work out *why* for the caller. This read only picks the error message;
        //    correctness was already decided by the UPDATE above.
        var result = await DiagnoseFailedHoldAsync(seatId, userId, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<bool> ReleaseHoldAsync(int seatId, Guid userId, CancellationToken ct = default)
    {
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE seats
            SET status = 'Available', held_by = NULL, hold_expires_at = NULL, version = version + 1
            WHERE id = {seatId} AND status = 'Held' AND held_by = {userId} AND hold_expires_at >= now()
            """, ct);

        return rows == 1;
    }

    public async Task<IReadOnlyList<SeatSnapshot>> GetSeatMapAsync(int eventId, CancellationToken ct = default)
    {
        // DateTime.UtcNow is translated to now() by Npgsql, so expiry is evaluated with the database clock.
        return await db.Seats
            .AsNoTracking()
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.Id)
            .Select(s => new SeatSnapshot(
                s.Id,
                s.VenueSeat!.Row,
                s.VenueSeat.Number,
                s.Status == SeatStatus.Held && s.HoldExpiresAt < DateTime.UtcNow ? SeatStatus.Available : s.Status,
                s.Version))
            .ToListAsync(ct);
    }

    private async Task<HoldResult> DiagnoseFailedHoldAsync(int seatId, Guid userId, CancellationToken ct)
    {
        var seat = await db.Seats
            .AsNoTracking()
            .Where(s => s.Id == seatId)
            .Select(s => new { s.Status, s.HeldBy, s.HoldExpiresAt })
            .FirstOrDefaultAsync(ct);

        if (seat is null)
            return new HoldResult(HoldOutcome.SeatNotFound);

        var activeHold = seat.Status == SeatStatus.Held && seat.HoldExpiresAt >= DateTime.UtcNow;

        // Re-holding your own seat is idempotent: return the existing hold instead of an error.
        if (activeHold && seat.HeldBy == userId)
            return new HoldResult(HoldOutcome.Held, seat.HoldExpiresAt);

        var seatWasClaimable = seat.Status == SeatStatus.Available || (seat.Status == SeatStatus.Held && !activeHold);
        return new HoldResult(seatWasClaimable ? HoldOutcome.HoldLimitReached : HoldOutcome.SeatUnavailable);
    }
}
