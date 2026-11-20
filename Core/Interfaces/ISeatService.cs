using Core.Models;

namespace Core.Interfaces;

public interface ISeatService
{
    /// <summary>
    /// Every seat of the showing: Booked/Available from Postgres, Held from Redis, plus the change counter
    /// the client needs to apply live updates on top. Seats held by <paramref name="viewerId"/> are flagged IsMine.
    /// </summary>
    Task<SeatMapSnapshot> GetSeatMapAsync(int eventId, Guid? viewerId, CancellationToken ct = default);
}
