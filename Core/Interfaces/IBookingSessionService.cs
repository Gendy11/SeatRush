using Core.Models;

namespace Core.Interfaces;

public interface IBookingSessionService
{
    Task<BookingSession> GetOrStartAsync(int eventId, Guid userId);

    Task<BookingSession?> GetActiveAsync(int eventId, Guid userId);

    Task EndAsync(int eventId, Guid userId);
}
