namespace SeatRush.DTOs;

public record EventDetailsDto(
    int Id,
    string MovieTitle,
    string PosterUrl,
    string BackdropUrl,
    string AgeRating,
    int DurationMinutes,
    string Venue,
    DateTime ShowTime,
    int TotalSeats,
    decimal TicketPrice);
