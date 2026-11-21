namespace Core.Entities;

/// <summary>One seat in a booking, with the price paid at booking time.</summary>
public class BookingItem : BaseEntity
{
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public int SeatId { get; set; }
    public Seat? Seat { get; set; }

    public decimal Price { get; set; }
}
