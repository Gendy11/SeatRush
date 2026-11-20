using Core.Entities;

namespace Core.Models;

/// <summary>
/// One seat status change, pushed live to everyone viewing the showing.
/// </summary>
/// <param name="Seq">Per-showing sequence number (Redis INCR). Clients ignore changes older than what they have.</param>
/// <param name="ExpiresAt">For Held: when the hold ends. Clients free the seat themselves at that time.</param>
public record SeatChange(int EventId, int SeatId, SeatStatus Status, DateTime? ExpiresAt, long Seq);
