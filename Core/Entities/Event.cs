namespace Core.Entities;

/// <summary>A showing in a venue (e.g. a film screening). Its seat inventory is copied from the venue layout.</summary>
public class Event : BaseEntity
{
    public required string Name { get; set; }
    public int VenueId { get; set; }
    public Venue? Venue { get; set; }

    /// <summary>Show time (UTC). Tickets are on sale as soon as the event exists.</summary>
    public DateTime ShowTime { get; set; }

    public List<Seat> Seats { get; set; } = [];

    public static Event For(Venue venue, string name, DateTime showTime)
    {
        if (venue.Seats.Count == 0)
            throw new InvalidOperationException("Venue seats must be loaded to create an event.");

        var ev = new Event { Name = name, Venue = venue, ShowTime = showTime };

        foreach (var venueSeat in venue.Seats)
            ev.Seats.Add(new Seat { VenueSeat = venueSeat, Status = SeatStatus.Available });

        return ev;
    }
}
