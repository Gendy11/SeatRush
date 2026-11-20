using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.SignalR;
using SeatRush.DTOs;

namespace SeatRush.SignalR;

/// <summary>
/// Sends seat changes to the showing's SignalR group. With the Redis backplane enabled, the message
/// reaches viewers connected to *any* API instance, not only this one.
/// </summary>
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
            // A failed push must never fail the hold/release itself: it already committed.
            // Clients re-sync the full map when they reconnect or refocus.
            logger.LogWarning(ex, "Failed to push seat change {@Change}", change);
        }
    }
}
