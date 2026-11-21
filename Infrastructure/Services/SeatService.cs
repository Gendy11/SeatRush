using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class SeatService(SeatRushContext db, ISeatHoldService holds) : ISeatService
{
    public async Task<SeatMapSnapshot> GetSeatMapAsync(int eventId, Guid? viewerId, CancellationToken ct = default)
    {
        // Read the change counter FIRST. Anything that changes after this point gets a higher Seq,
        // so the client can safely apply those live updates on top of this snapshot.
        var seq = await holds.GetSeqAsync(eventId);

        var seats = await db.Seats
            .AsNoTracking()
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.VenueSeat!.Row, s.VenueSeat.Number, s.Status })
            .ToListAsync(ct);

        var activeHolds = await holds.GetActiveHoldsAsync(eventId);

        var snapshot = seats
            .Select(s =>
            {
                // Booked (Postgres) wins over any leftover hold; otherwise an active Redis hold means Held.
                if (s.Status != SeatStatus.Booked && activeHolds.TryGetValue(s.Id, out var hold))
                    return new SeatSnapshot(s.Id, s.Row, s.Number, SeatStatus.Held, hold.ExpiresAt, hold.HolderId == viewerId);

                return new SeatSnapshot(s.Id, s.Row, s.Number, s.Status, HoldExpiresAt: null, IsMine: false);
            })
            .ToList();

        return new SeatMapSnapshot(seq, snapshot);
    }
}
