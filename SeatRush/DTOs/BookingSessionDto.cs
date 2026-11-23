namespace SeatRush.DTOs;

public record BookingSessionDto(int EventId, DateTime ExpiresAt, DateTime ServerNow);
