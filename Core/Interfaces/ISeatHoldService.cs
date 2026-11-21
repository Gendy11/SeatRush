using Core.Models;

namespace Core.Interfaces;

/// <summary>Temporary seat holds, stored in Redis with a TTL that ends with the user's booking session.</summary>
public interface ISeatHoldService
{
    /// <summary>
    /// Holds the seat until the user's session ends. Exactly one concurrent caller can win a seat.
    /// Holding a seat you already hold succeeds again (idempotent).
    /// </summary>
    Task<HoldResult> HoldAsync(int seatId, Guid userId, CancellationToken ct = default);

    /// <summary>Releases the seat only if this user holds it. Returns false otherwise.</summary>
    Task<bool> ReleaseAsync(int seatId, Guid userId, CancellationToken ct = default);

    /// <summary>Active holds of a showing: seat id → holder and expiry.</summary>
    Task<IReadOnlyDictionary<int, ActiveHold>> GetActiveHoldsAsync(int eventId);

    /// <summary>The showing's current change counter (see <see cref="SeatMapSnapshot.Seq"/>).</summary>
    Task<long> GetSeqAsync(int eventId);

    /// <summary>True if every seat is currently held by this user.</summary>
    Task<bool> AreAllHeldByAsync(int eventId, IReadOnlyCollection<int> seatIds, Guid userId);

    /// <summary>
    /// After a booking commits: removes the seats' holds and pushes "Booked" to live viewers.
    /// </summary>
    Task MarkBookedAsync(int eventId, IReadOnlyCollection<int> seatIds);
}
