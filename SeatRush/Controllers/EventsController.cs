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

    /// <summary>
    /// FR10: start (or resume) the user's 7-minute booking session for this showing.
    /// Idempotent: refreshing the page returns the same deadline.
    /// </summary>
    [HttpPost("{eventId:int}/session")]
    public async Task<ActionResult<BookingSessionDto>> StartSession(
        int eventId,
        [FromServices] IBookingSessionService sessions,
        [FromServices] TimeProvider clock,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return MissingUser();

        var showTime = await db.Events
            .Where(e => e.Id == eventId)
            .Select(e => (DateTime?)e.ShowTime)
            .FirstOrDefaultAsync(ct);

        if (showTime is null)
            return NotFound();

        if (showTime <= clock.GetUtcNow().UtcDateTime)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This showing has already started.");

        var session = await sessions.GetOrStartAsync(eventId, userId);
        return Ok(new BookingSessionDto(eventId, session.ExpiresAt, clock.GetUtcNow().UtcDateTime));
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

    /// <summary>
    /// FR5: every seat with its status (Booked from Postgres, Held from Redis).
    /// Seats held by the caller (X-User-Id) are flagged isMine.
    /// </summary>
    [HttpGet("{eventId:int}/seats")]
    public async Task<ActionResult<SeatMapDto>> GetSeatMap(int eventId, [FromServices] TimeProvider clock, CancellationToken ct)
    {
        if (!await db.Events.AnyAsync(e => e.Id == eventId, ct))
            return NotFound();

        Guid? viewer = TryGetUserId(out var userId) ? userId : null;
        var map = await seats.GetSeatMapAsync(eventId, viewer, ct);

        return Ok(new SeatMapDto(
            map.Seq,
            clock.GetUtcNow().UtcDateTime,
            map.Seats.Select(s => new SeatDto(s.Id, s.Row, s.Number, s.Status.ToString(), s.HoldExpiresAt, s.IsMine)).ToList()));
    }
}
