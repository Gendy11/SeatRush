namespace Core.Entities;

/// <summary>A film. Shown many times across venues as Events (showings).</summary>
public class Movie : BaseEntity
{
    public required string Title { get; set; }

    /// <summary>Portrait poster (path served by the frontend, e.g. "movies/x-poster.jpg"): cards, checkout, tickets.</summary>
    public required string PosterUrl { get; set; }

    /// <summary>Landscape still (path served by the frontend): fills the 3D hall's screen and the home page hero.</summary>
    public required string BackdropUrl { get; set; }

    /// <summary>Age rating label, e.g. "PG-13", "R".</summary>
    public required string AgeRating { get; set; }

    public int DurationMinutes { get; set; }
}
