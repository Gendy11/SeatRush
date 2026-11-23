using Core.Entities;
using Core.Enums;

namespace Core.Models;

public record SeatChange(int EventId, int SeatId, SeatStatus Status, DateTime? ExpiresAt, long Seq);
