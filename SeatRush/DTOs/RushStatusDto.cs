namespace SeatRush.DTOs;

public record RushStatusDto(
    bool Running,
    string? Mode,
    int Bots,
    int ActiveBots,
    int SeatsBooked,
    DateTime? StartedAt,
    long? QueuePosition = null);
