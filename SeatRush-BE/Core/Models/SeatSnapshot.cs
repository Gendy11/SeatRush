using Core.Entities;

namespace Core.Models;

/// <summary>Read model for the seat map. Status already has lazy expiry applied.</summary>
public record SeatSnapshot(int Id, string Row, int Number, SeatStatus Status, int Version);
