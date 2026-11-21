using Core.Enums;

namespace Core.Entities;

/// <summary>
/// The sellable copy of a venue seat for one event. In Postgres a seat is only Available or Booked;
/// temporary holds live in Redis (see ISeatHoldService) and are merged in when the seat map is read.
/// </summary>
public class Seat : BaseEntity
{
    public int EventId { get; set; }
    public Event? Event { get; set; }

    public int VenueSeatId { get; set; }
    public VenueSeat? VenueSeat { get; set; }

    public SeatStatus Status { get; set; }

    /// <summary>Incremented on every status change (real-time ordering + optimistic concurrency token).</summary>
    public int Version { get; set; }
}
