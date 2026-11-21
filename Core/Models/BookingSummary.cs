using Core.Entities;
using Core.Enums;

namespace Core.Models;

public record BookedSeat(int SeatId, string Row, int Number, decimal Price);

/// <summary>A booking as shown on the receipt and in "My bookings".</summary>
public record BookingSummary(
    int Id,
    string Reference,
    int EventId,
    string MovieTitle,
    string PosterUrl,
    string Venue,
    DateTime ShowTime,
    IReadOnlyList<BookedSeat> Seats,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    BookingStatus Status,
    DateTime CreatedAt);

public enum CheckoutOutcome
{
    Booked,

    /// <summary>Same Idempotency-Key as an earlier checkout: the original booking is returned.</summary>
    Replayed,

    NoSeats,

    /// <summary>At least one seat is not (or no longer) held by this user, e.g. the 7 minutes ran out.</summary>
    SeatsNotHeld,

    /// <summary>At least one seat was booked by someone else (Postgres rejected the update).</summary>
    SeatsUnavailable
}

public record CheckoutResult(CheckoutOutcome Outcome, BookingSummary? Booking = null);
