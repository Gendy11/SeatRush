namespace SeatRush.DTOs;

/// <summary>A movie with its upcoming showings: the shape the "Now Showing" page needs.</summary>
public record MovieDto(
    int Id,
    string Title,
    string PosterUrl,
    string AgeRating,
    int DurationMinutes,
    IReadOnlyList<ShowingDto> Showings);

public record ShowingDto(int EventId, string Venue, DateTime ShowTime);
