namespace Core.Models;

public record BookingSession(int EventId, Guid UserId, DateTime ExpiresAt);
