namespace Core.Entities;

/// <summary>A film. Shown many times across venues as Events (showings).</summary>
public class Movie : BaseEntity
{
    public required string Title { get; set; }

    /// <summary>Poster image path or URL, e.g. "posters/neon-harbor.svg" (served by the frontend).</summary>
    public required string PosterUrl { get; set; }

    /// <summary>Age rating label, e.g. "PG", "12+", "18+".</summary>
    public required string AgeRating { get; set; }

    public int DurationMinutes { get; set; }
}
