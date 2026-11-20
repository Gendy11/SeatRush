namespace Core.Entities;

/// <summary>A physical seat in a venue (layout only, no sale status).</summary>
public class VenueSeat : BaseEntity
{
    public int VenueId { get; set; }
    public Venue? Venue { get; set; }

    public required string Row { get; set; }
    public int Number { get; set; }
}
