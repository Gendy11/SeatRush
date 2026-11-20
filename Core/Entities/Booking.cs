namespace Core.Entities;

public class Booking : BaseEntity
{
    public int SeatId { get; set; }
    public Seat? Seat { get; set; }

    public Guid UserId { get; set; }
    public required string IdempotencyKey { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
