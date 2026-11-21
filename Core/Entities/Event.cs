using Core.Enums;

namespace Core.Entities;

/// <summary>A showing of a movie in a venue at a show time. Its seat inventory is copied from the venue layout.</summary>
public class Event : BaseEntity
{
    public int MovieId { get; set; }
    public Movie? Movie { get; set; }

    public int VenueId { get; set; }
    public Venue? Venue { get; set; }

    /// <summary>Show time (UTC). Tickets are on sale as soon as the event exists.</summary>
    public DateTime ShowTime { get; set; }

    public List<Seat> Seats { get; set; } = [];

    public static Event For(Movie movie, Venue venue, DateTime showTime)
    {
        if (venue.Seats.Count == 0)
            throw new InvalidOperationException("Venue seats must be loaded to create an event.");

        var ev = new Event { Movie = movie, Venue = venue, ShowTime = showTime };

        foreach (var venueSeat in venue.Seats)
            ev.Seats.Add(new Seat { VenueSeat = venueSeat, Status = SeatStatus.Available });

        return ev;
    }
}
