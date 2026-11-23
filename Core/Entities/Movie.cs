namespace Core.Entities;

public class Movie : BaseEntity
{
    public required string Title { get; set; }

    public required string PosterUrl { get; set; }

    public required string BackdropUrl { get; set; }

    public required string AgeRating { get; set; }

    public int DurationMinutes { get; set; }
}
