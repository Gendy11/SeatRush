using Core.Models;

namespace Core.Interfaces;

public interface ISeatMapNotifier
{
    Task SeatChangedAsync(SeatChange change);

    Task ShowingResetAsync(int eventId);
}
