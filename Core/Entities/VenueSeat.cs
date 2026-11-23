namespace Core.Entities;

public class VenueSeat : BaseEntity
{
    public int VenueId { get; set; }
    public Venue? Venue { get; set; }

    public required string Row { get; set; }
    public int Number { get; set; }
}
