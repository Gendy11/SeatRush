using Core.Enums;

namespace Core.Entities;

/// <summary>One checkout: a user's seats for one showing.</summary>
public class Booking : BaseEntity
{
    /// <summary>Short code shown at the counter, e.g. "SR-7K3Q9P".</summary>
    public required string Reference { get; set; }

    public int EventId { get; set; }
    public Event? Event { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Client-generated key; a retried checkout with the same key returns this booking instead of a new one.</summary>
    public required string IdempotencyKey { get; set; }

    public BookingStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<BookingItem> Items { get; set; } = [];
}
