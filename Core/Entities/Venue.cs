namespace Core.Entities;

/// <summary>A physical hall/screen with a fixed seat layout, reused by many events.</summary>
public class Venue : BaseEntity
{
    public required string Name { get; set; }
    public int Capacity { get; set; }

    /// <summary>Price of one ticket for any showing in this venue (EGP).</summary>
    public decimal TicketPrice { get; set; }

    public List<VenueSeat> Seats { get; set; } = [];

    public static Venue WithLayout(string name, int rows, int seatsPerRow, decimal ticketPrice)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(seatsPerRow, 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticketPrice);

        var venue = new Venue { Name = name, Capacity = rows * seatsPerRow, TicketPrice = ticketPrice };

        for (var r = 0; r < rows; r++)
        {
            var rowLabel = RowLabel(r);
            for (var n = 1; n <= seatsPerRow; n++)
                venue.Seats.Add(new VenueSeat { Row = rowLabel, Number = n });
        }

        return venue;
    }

    // 0 -> A, 25 -> Z, 26 -> AA ...
    private static string RowLabel(int index)
    {
        var label = "";
        index++;
        while (index > 0)
        {
            index--;
            label = (char)('A' + index % 26) + label;
            index /= 26;
        }
        return label;
    }
}
