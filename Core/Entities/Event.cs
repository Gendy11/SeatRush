using Core.Enums;

namespace Core.Entities;

public class Event : BaseEntity
{
    public int MovieId { get; set; }
    public Movie? Movie { get; set; }

    public int VenueId { get; set; }
    public Venue? Venue { get; set; }

    public DateTime ShowTime { get; set; }

    public bool HasWaitingRoom { get; set; }

    public List<Seat> Seats { get; set; } = [];

    public static Event For(Movie movie, Venue venue, DateTime showTime, bool hasWaitingRoom = false)
    {
        if (venue.Seats.Count == 0)
            throw new InvalidOperationException("Venue seats must be loaded to create an event.");

        var ev = new Event { Movie = movie, Venue = venue, ShowTime = showTime, HasWaitingRoom = hasWaitingRoom };

        foreach (var venueSeat in venue.Seats)
            ev.Seats.Add(new Seat { VenueSeat = venueSeat, Status = SeatStatus.Available });

        return ev;
    }
}
