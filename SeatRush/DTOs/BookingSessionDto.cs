namespace SeatRush.DTOs;

/// <param name="ServerNow">Lets the client correct for its own clock being off when drawing the countdown.</param>
public record BookingSessionDto(int EventId, DateTime ExpiresAt, DateTime ServerNow);
