namespace SeatRush.DTOs;

public record QueueStatusDto(string State, long? Position, DateTime? AdmittedUntil, DateTime ServerNow);
