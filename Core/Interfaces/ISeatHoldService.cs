using Core.Models;

namespace Core.Interfaces;

public interface ISeatHoldService
{
    Task<HoldResult> HoldAsync(int seatId, Guid userId, CancellationToken ct = default);

    Task<bool> ReleaseAsync(int seatId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<int, ActiveHold>> GetActiveHoldsAsync(int eventId);

    Task<long> GetSeqAsync(int eventId);

    Task<bool> AreAllHeldByAsync(int eventId, IReadOnlyCollection<int> seatIds, Guid userId);

    Task MarkBookedAsync(int eventId, IReadOnlyCollection<int> seatIds);
}
