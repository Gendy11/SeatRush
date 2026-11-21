using Core.Entities;
using Core.Enums;

namespace Core.Models;

/// <summary>Read model for one seat: Postgres status merged with active Redis holds.</summary>
/// <param name="HoldExpiresAt">For Held seats: when the hold ends (clients free the seat themselves then).</param>
/// <param name="IsMine">True when the seat is held by the user viewing the map.</param>
public record SeatSnapshot(int Id, string Row, int Number, SeatStatus Status, DateTime? HoldExpiresAt, bool IsMine);

/// <param name="Seq">
/// The showing's change counter, read *before* the seats. Live changes with Seq &lt;= this are already
/// reflected in the snapshot; newer ones must be applied on top.
/// </param>
public record SeatMapSnapshot(long Seq, IReadOnlyList<SeatSnapshot> Seats);
