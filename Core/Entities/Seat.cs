using Core.Enums;

namespace Core.Entities;

public class Seat : BaseEntity
{
    public int EventId { get; set; }
    public Event? Event { get; set; }

    public int VenueSeatId { get; set; }
    public VenueSeat? VenueSeat { get; set; }

    public SeatStatus Status { get; set; }

    public int Version { get; set; }
}
