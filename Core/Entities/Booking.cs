using Core.Enums;

namespace Core.Entities;

public class Booking : BaseEntity
{
    public required string Reference { get; set; }

    public int EventId { get; set; }
    public Event? Event { get; set; }

    public Guid UserId { get; set; }

    public required string IdempotencyKey { get; set; }

    public BookingStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<BookingItem> Items { get; set; } = [];
}
