namespace SeatRush.DTOs;

/// <param name="HoldExpiresAt">For Held seats: when the hold ends. The client frees the seat itself then.</param>
/// <param name="IsMine">True when the seat is held by the requesting user.</param>
public record SeatDto(int Id, string Row, int Number, string Status, DateTime? HoldExpiresAt, bool IsMine);

/// <param name="Seq">Change counter at snapshot time; apply live changes with a higher Seq on top.</param>
/// <param name="ServerNow">Lets the client correct for its own clock when expiring holds.</param>
public record SeatMapDto(long Seq, DateTime ServerNow, IReadOnlyList<SeatDto> Seats);

/// <summary>Live message sent over SignalR ("SeatChanged").</summary>
public record SeatChangeDto(int SeatId, string Status, DateTime? ExpiresAt, long Seq);
