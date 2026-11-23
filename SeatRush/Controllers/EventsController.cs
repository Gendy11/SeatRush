using Core.Interfaces;
using Core.Models;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatRush.DTOs;

namespace SeatRush.Controllers;

public class EventsController(SeatRushContext db, ISeatService seats, IWaitingRoomService waitingRoom) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EventDto>>> GetEvents(CancellationToken ct)
    {
        var events = await db.Events
            .AsNoTracking()
            .Where(e => e.ShowTime > DateTime.UtcNow)
            .OrderBy(e => e.ShowTime)
            .Select(e => new EventDto(e.Id, e.MovieId, e.Movie!.Title, e.Venue!.Name, e.ShowTime, e.Venue.Capacity))
            .ToListAsync(ct);

        return Ok(events);
    }

    [Authorize]
    [HttpPost("{eventId:int}/session")]
    public async Task<ActionResult<BookingSessionDto>> StartSession(
        int eventId,
        [FromServices] IBookingSessionService sessions,
        [FromServices] TimeProvider clock,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return MissingUser();

        var ev = await db.Events
            .Where(e => e.Id == eventId)
            .Select(e => new { e.ShowTime, e.HasWaitingRoom })
            .FirstOrDefaultAsync(ct);

        if (ev is null)
            return NotFound();

        if (ev.ShowTime <= clock.GetUtcNow().UtcDateTime)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This showing has already started.");

        if (ev.HasWaitingRoom
            && await sessions.GetActiveAsync(eventId, userId) is null
            && !await waitingRoom.IsAdmittedAsync(eventId, userId))
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Join the waiting room first.");
        }

        var session = await sessions.GetOrStartAsync(eventId, userId);

        if (ev.HasWaitingRoom)
            await waitingRoom.EnterAsync(eventId, userId, session.ExpiresAt);

        return Ok(new BookingSessionDto(eventId, session.ExpiresAt, clock.GetUtcNow().UtcDateTime));
    }

    [Authorize]
    [HttpPost("{eventId:int}/queue")]
    public async Task<ActionResult<QueueStatusDto>> JoinQueue(int eventId, [FromServices] TimeProvider clock, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return MissingUser();

        var ev = await db.Events
            .Where(e => e.Id == eventId)
            .Select(e => new { e.ShowTime, e.HasWaitingRoom })
            .FirstOrDefaultAsync(ct);

        if (ev is null)
            return NotFound();

        if (ev.ShowTime <= clock.GetUtcNow().UtcDateTime)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This showing has already started.");

        var status = ev.HasWaitingRoom
            ? await waitingRoom.JoinAsync(eventId, userId)
            : new QueueStatus(QueueState.Admitted);

        return Ok(ToDto(status, clock));
    }

    [Authorize]
    [HttpGet("{eventId:int}/queue")]
    public async Task<ActionResult<QueueStatusDto>> GetQueueStatus(int eventId, [FromServices] TimeProvider clock)
    {
        if (!TryGetUserId(out var userId))
            return MissingUser();

        return Ok(ToDto(await waitingRoom.GetStatusAsync(eventId, userId), clock));
    }

    private static QueueStatusDto ToDto(QueueStatus s, TimeProvider clock) =>
        new(s.State.ToString(), s.Position, s.AdmittedUntil, clock.GetUtcNow().UtcDateTime);

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
                e.Movie.BackdropUrl,
                e.Movie.AgeRating,
                e.Movie.DurationMinutes,
                e.Venue!.Name,
                e.ShowTime,
                e.Venue.Capacity,
                e.Venue.TicketPrice,
                e.HasWaitingRoom))
            .FirstOrDefaultAsync(ct);

        return ev is null ? NotFound() : Ok(ev);
    }

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
