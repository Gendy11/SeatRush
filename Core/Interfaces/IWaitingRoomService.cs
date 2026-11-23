using Core.Models;

namespace Core.Interfaces;

public interface IWaitingRoomService
{
    Task<QueueStatus> JoinAsync(int eventId, Guid userId);

    Task<QueueStatus> GetStatusAsync(int eventId, Guid userId);

    Task<bool> IsAdmittedAsync(int eventId, Guid userId);

    Task EnterAsync(int eventId, Guid userId, DateTime sessionExpiresAt);

    Task LeaveAsync(int eventId, Guid userId);

    Task LeaveLineAsync(int eventId, Guid userId);

    Task<int> AdmitAsync(int eventId);

    Task AdmitAllAsync();
}
