namespace Core.Entities;

/// <summary>A physical hall/screen with a fixed seat layout, reused by many events.</summary>
public class Venue : BaseEntity
{
    public required string Name { get; set; }
    public int Capacity { get; set; }

    public List<VenueSeat> Seats { get; set; } = [];

    public static Venue WithLayout(string name, int rows, int seatsPerRow)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(seatsPerRow, 1);

        var venue = new Venue { Name = name, Capacity = rows * seatsPerRow };

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
