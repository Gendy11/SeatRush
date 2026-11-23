using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.SignalR;
using SeatRush.DTOs;

namespace SeatRush.SignalR;

public class SeatMapNotifier(IHubContext<SeatMapHub> hub, ILogger<SeatMapNotifier> logger) : ISeatMapNotifier
{
    public async Task SeatChangedAsync(SeatChange change)
    {
        var message = new SeatChangeDto(change.SeatId, change.Status.ToString(), change.ExpiresAt, change.Seq);

        try
        {
            await hub.Clients.Group(SeatMapHub.Group(change.EventId)).SendAsync(SeatMapHub.SeatChangedMethod, message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push seat change {@Change}", change);
        }
    }

    public async Task ShowingResetAsync(int eventId)
    {
        try
        {
            await hub.Clients.Group(SeatMapHub.Group(eventId)).SendAsync(SeatMapHub.ShowingResetMethod, eventId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push reset of event {EventId}", eventId);
        }
    }
}
