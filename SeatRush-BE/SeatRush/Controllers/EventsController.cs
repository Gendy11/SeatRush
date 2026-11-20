using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatRush.DTOs;

namespace SeatRush.Controllers;

public class EventsController(SeatRushContext db, ISeatService seats) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EventDto>>> GetEvents(CancellationToken ct)
    {
        var events = await db.Events
            .AsNoTracking()
            .OrderBy(e => e.ShowTime)
            .Select(e => new EventDto(e.Id, e.Name, e.Venue!.Name, e.ShowTime, e.Venue.Capacity))
            .ToListAsync(ct);

        return Ok(events);
    }

    [HttpGet("{eventId:int}/seats")]
    public async Task<ActionResult<IReadOnlyList<SeatDto>>> GetSeatMap(int eventId, CancellationToken ct)
    {
        if (!await db.Events.AnyAsync(e => e.Id == eventId, ct))
            return NotFound();

        var map = await seats.GetSeatMapAsync(eventId, ct);
        return Ok(map.Select(s => new SeatDto(s.Id, s.Row, s.Number, s.Status.ToString(), s.Version)));
    }
}
