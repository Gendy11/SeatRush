using System.Collections.Concurrent;
using Core.Interfaces;
using Core.Models;

namespace IntegrationTests.Infrastructure;

/// <summary>Captures the live updates the services would push over SignalR.</summary>
public class RecordingNotifier : ISeatMapNotifier
{
    public ConcurrentQueue<SeatChange> Changes { get; } = new();

    public Task SeatChangedAsync(SeatChange change)
    {
        Changes.Enqueue(change);
        return Task.CompletedTask;
    }

    public ConcurrentQueue<int> Resets { get; } = new();

    public Task ShowingResetAsync(int eventId)
    {
        Resets.Enqueue(eventId);
        return Task.CompletedTask;
    }

    public IReadOnlyList<SeatChange> For(int eventId) => Changes.Where(c => c.EventId == eventId).OrderBy(c => c.Seq).ToList();
}
