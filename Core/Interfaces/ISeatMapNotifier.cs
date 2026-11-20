using Core.Models;

namespace Core.Interfaces;

/// <summary>Pushes seat changes to live viewers of a showing (implemented with SignalR in the API).</summary>
public interface ISeatMapNotifier
{
    Task SeatChangedAsync(SeatChange change);
}
