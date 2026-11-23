using Microsoft.AspNetCore.SignalR;

namespace SeatRush.SignalR;

public class SeatMapHub : Hub
{
    public const string Path = "/hubs/seats";
    public const string SeatChangedMethod = "SeatChanged";
    public const string ShowingResetMethod = "ShowingReset";

    public static string Group(int eventId) => $"event-{eventId}";

    public Task JoinEvent(int eventId) => Groups.AddToGroupAsync(Context.ConnectionId, Group(eventId));

    public Task LeaveEvent(int eventId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(eventId));
}
