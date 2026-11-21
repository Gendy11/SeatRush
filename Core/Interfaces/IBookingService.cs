using Core.Entities;
using Core.Enums;
using Core.Models;

namespace Core.Interfaces;

public interface IBookingService
{
    /// <summary>
    /// Turns the user's held seats into a booking. Postgres has the final say: seats are only booked
    /// if they are still Available, so a seat can never be sold twice. Retrying with the same
    /// idempotency key returns the original booking.
    /// </summary>
    Task<CheckoutResult> CheckoutAsync(
        int eventId,
        Guid userId,
        IReadOnlyCollection<int> seatIds,
        PaymentMethod paymentMethod,
        string idempotencyKey,
        CancellationToken ct = default);

    /// <summary>The user's bookings, newest first.</summary>
    Task<IReadOnlyList<BookingSummary>> GetBookingsAsync(Guid userId, CancellationToken ct = default);
}
