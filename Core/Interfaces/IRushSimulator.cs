using Core.Models;

namespace Core.Interfaces;

public interface IRushSimulator
{
    Task<RushStartResult> StartAsync(int eventId, Guid startedBy, CancellationToken ct = default);

    Task StopAsync(int eventId);

    RushStatus GetStatus(int eventId);
}

public interface IShowingResetService
{
    Task<bool> ResetAsync(int eventId, CancellationToken ct = default);
}
