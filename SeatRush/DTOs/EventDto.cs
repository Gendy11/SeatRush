namespace SeatRush.DTOs;

public record EventDto(int Id, int MovieId, string MovieTitle, string Venue, DateTime ShowTime, int TotalSeats);
