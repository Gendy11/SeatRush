using Core.Entities;
using Core.Enums;
using Core.Models;

namespace Core.Interfaces;

public interface IBookingService
{
    Task<CheckoutResult> CheckoutAsync(
        int eventId,
        Guid userId,
        IReadOnlyCollection<int> seatIds,
        PaymentMethod paymentMethod,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<IReadOnlyList<BookingSummary>> GetBookingsAsync(Guid userId, CancellationToken ct = default);
}
