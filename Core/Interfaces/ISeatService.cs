using Core.Models;

namespace Core.Interfaces;

public interface ISeatService
{
    Task<SeatMapSnapshot> GetSeatMapAsync(int eventId, Guid? viewerId, CancellationToken ct = default);
}
