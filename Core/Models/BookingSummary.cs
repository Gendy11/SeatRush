using Core.Entities;
using Core.Enums;

namespace Core.Models;

public record BookedSeat(int SeatId, string Row, int Number, decimal Price);

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

    Replayed,

    NoSeats,

    SeatsNotHeld,

    SeatsUnavailable
}

public record CheckoutResult(CheckoutOutcome Outcome, BookingSummary? Booking = null);
