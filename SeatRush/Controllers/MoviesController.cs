using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatRush.DTOs;

namespace SeatRush.Controllers;

public class MoviesController(SeatRushContext db) : BaseApiController
{
    /// <summary>FR4: movies that have upcoming showings, each with its showings soonest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MovieDto>>> GetMovies(CancellationToken ct)
    {
        // One query: upcoming showings with their movie and venue, grouped in memory (small result set).
        var showings = await db.Events
            .AsNoTracking()
            .Where(e => e.ShowTime > DateTime.UtcNow) // translated to now() in Postgres
            .OrderBy(e => e.ShowTime)
            .Select(e => new
            {
                e.Id,
                e.ShowTime,
                Venue = e.Venue!.Name,
                Movie = e.Movie!
            })
            .ToListAsync(ct);

        var movies = showings
            .GroupBy(s => s.Movie.Id)
            .Select(g =>
            {
                var m = g.First().Movie;
                return new MovieDto(
                    m.Id, m.Title, m.PosterUrl, m.AgeRating, m.DurationMinutes,
                    g.Select(s => new ShowingDto(s.Id, s.Venue, s.ShowTime)).ToList());
            })
            .ToList();

        return Ok(movies);
    }
}
