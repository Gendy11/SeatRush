using Core.Entities;
using Core.Enums;

namespace Core.Models;

public record SeatSnapshot(int Id, string Row, int Number, SeatStatus Status, DateTime? HoldExpiresAt, bool IsMine);

public record SeatMapSnapshot(long Seq, IReadOnlyList<SeatSnapshot> Seats);
