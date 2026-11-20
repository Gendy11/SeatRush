namespace Core.Entities;

/// <summary>The sellable copy of a venue seat for one event. This is the row holds/bookings contend on.</summary>
public class Seat : BaseEntity
{
    public int EventId { get; set; }
    public Event? Event { get; set; }

    public int VenueSeatId { get; set; }
    public VenueSeat? VenueSeat { get; set; }

    public SeatStatus Status { get; set; }

    public Guid? HeldBy { get; set; }

    public DateTime? HoldExpiresAt { get; set; }

    /// <summary>Incremented on every status change (real-time ordering + optimistic concurrency token).</summary>
    public int Version { get; set; }
}
