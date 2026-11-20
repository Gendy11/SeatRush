using Core.Models;

namespace Core.Interfaces;

public interface ISeatService
{
    /// <summary>
    /// Atomically holds a seat for the user. Exactly one concurrent caller can win a given seat.
    /// Holding a seat the user already holds is idempotent and returns the existing expiry.
    /// </summary>
    Task<HoldResult> HoldSeatAsync(int seatId, Guid userId, CancellationToken ct = default);

    /// <summary>Releases the user's active hold. Returns false if the user does not hold the seat.</summary>
    Task<bool> ReleaseHoldAsync(int seatId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<SeatSnapshot>> GetSeatMapAsync(int eventId, CancellationToken ct = default);
}
