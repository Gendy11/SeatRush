namespace SeatRush.DTOs;

public record MovieDto(
    int Id,
    string Title,
    string PosterUrl,
    string BackdropUrl,
    string AgeRating,
    int DurationMinutes,
    IReadOnlyList<ShowingDto> Showings);

public record ShowingDto(int EventId, string Venue, DateTime ShowTime);
