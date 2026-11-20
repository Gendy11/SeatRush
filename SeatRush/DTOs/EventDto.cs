namespace SeatRush.DTOs;

public record EventDto(int Id, string Name, string Venue, DateTime ShowTime, int TotalSeats);
