using Microsoft.AspNetCore.SignalR;

namespace SeatRush.SignalR;

/// <summary>
/// Live seat map. A viewer joins the group of the showing they are looking at and receives
/// "SeatChanged" messages for it. The hub only manages groups; changes are pushed by SeatMapNotifier.
/// </summary>
public class SeatMapHub : Hub
{
    public const string Path = "/hubs/seats";
    public const string SeatChangedMethod = "SeatChanged";

    public static string Group(int eventId) => $"event-{eventId}";

    public Task JoinEvent(int eventId) => Groups.AddToGroupAsync(Context.ConnectionId, Group(eventId));

    public Task LeaveEvent(int eventId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(eventId));
}
