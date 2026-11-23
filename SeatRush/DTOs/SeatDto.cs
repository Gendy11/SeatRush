namespace SeatRush.DTOs;

public record SeatDto(int Id, string Row, int Number, string Status, DateTime? HoldExpiresAt, bool IsMine);

public record SeatMapDto(long Seq, DateTime ServerNow, IReadOnlyList<SeatDto> Seats);

public record SeatChangeDto(int SeatId, string Status, DateTime? ExpiresAt, long Seq);
