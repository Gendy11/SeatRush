using Core.Models;

namespace Core.Interfaces;

public interface IBookingSessionService
{
    /// <summary>
    /// Returns the user's active session for the showing, or starts a new one.
    /// Calling it again (refresh, second tab) returns the same deadline.
    /// </summary>
    Task<BookingSession> GetOrStartAsync(int eventId, Guid userId);

    /// <summary>The user's active session, or null if none / expired.</summary>
    Task<BookingSession?> GetActiveAsync(int eventId, Guid userId);

    /// <summary>Ends the session early (after a successful booking).</summary>
    Task EndAsync(int eventId, Guid userId);
}
