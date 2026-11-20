namespace SeatRush.DTOs;

public record EventDetailsDto(
    int Id,
    string MovieTitle,
    string PosterUrl,
    string AgeRating,
    int DurationMinutes,
    string Venue,
    DateTime ShowTime,
    int TotalSeats);
