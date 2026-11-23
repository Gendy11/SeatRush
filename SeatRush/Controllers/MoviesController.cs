using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatRush.DTOs;

namespace SeatRush.Controllers;

public class MoviesController(SeatRushContext db) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MovieDto>>> GetMovies(CancellationToken ct)
    {
        var showings = await db.Events
            .AsNoTracking()
            .Where(e => e.ShowTime > DateTime.UtcNow)
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
                    m.Id, m.Title, m.PosterUrl, m.BackdropUrl, m.AgeRating, m.DurationMinutes,
                    g.Select(s => new ShowingDto(s.Id, s.Venue, s.ShowTime)).ToList());
            })
            .ToList();

        return Ok(movies);
    }
}
