using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatRush.DTOs;

namespace SeatRush.Controllers;

public class EventsController(SeatRushContext db, ISeatService seats) : BaseApiController
{
    /// <summary>FR4: upcoming showings only, soonest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EventDto>>> GetEvents(CancellationToken ct)
    {
        var events = await db.Events
            .AsNoTracking()
            .Where(e => e.ShowTime > DateTime.UtcNow) // translated to now() in Postgres
            .OrderBy(e => e.ShowTime)
            .Select(e => new EventDto(e.Id, e.MovieId, e.Movie!.Title, e.Venue!.Name, e.ShowTime, e.Venue.Capacity))
            .ToListAsync(ct);

        return Ok(events);
    }

    /// <summary>One showing with its movie and venue details (header of the seat-map page).</summary>
    [HttpGet("{eventId:int}")]
    public async Task<ActionResult<EventDetailsDto>> GetEvent(int eventId, CancellationToken ct)
    {
        var ev = await db.Events
            .AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new EventDetailsDto(
                e.Id,
                e.Movie!.Title,
                e.Movie.PosterUrl,
                e.Movie.AgeRating,
                e.Movie.DurationMinutes,
                e.Venue!.Name,
                e.ShowTime,
                e.Venue.Capacity))
            .FirstOrDefaultAsync(ct);

        return ev is null ? NotFound() : Ok(ev);
    }

    /// <summary>FR5: every seat of the showing with its status. Expired holds are reported as Available.</summary>
    [HttpGet("{eventId:int}/seats")]
    public async Task<ActionResult<IReadOnlyList<SeatDto>>> GetSeatMap(int eventId, CancellationToken ct)
    {
        if (!await db.Events.AnyAsync(e => e.Id == eventId, ct))
            return NotFound();

        var map = await seats.GetSeatMapAsync(eventId, ct);
        return Ok(map.Select(s => new SeatDto(s.Id, s.Row, s.Number, s.Status.ToString(), s.Version)));
    }
}
