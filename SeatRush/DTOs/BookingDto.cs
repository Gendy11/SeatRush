using Core.Enums;

namespace SeatRush.DTOs;

public record CreateBookingDto(int EventId, IReadOnlyList<int> SeatIds, string PaymentMethod);

public record BookedSeatDto(int SeatId, string Row, int Number, decimal Price);

public record BookingDto(
    int Id,
    string Reference,
    int EventId,
    string MovieTitle,
    string PosterUrl,
    string Venue,
    DateTime ShowTime,
    IReadOnlyList<BookedSeatDto> Seats,
    decimal TotalAmount,
    string PaymentMethod,
    string Status,
    DateTime CreatedAt);
