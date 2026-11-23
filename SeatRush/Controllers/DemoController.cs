using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatRush.DTOs;

namespace SeatRush.Controllers;

[Authorize]
[Route("api/demo/events/{eventId:int}")]
public class DemoController(IRushSimulator rush) : BaseApiController
{
    [HttpPost("rush")]
    public async Task<ActionResult<RushStatusDto>> Start(int eventId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return MissingUser();

        var result = await rush.StartAsync(eventId, userId, ct);

        return result.Outcome switch
        {
            RushStartOutcome.Started => Accepted(ToDto(rush.GetStatus(eventId), result.QueuePosition)),
            RushStartOutcome.NotFound => NotFound(),
            RushStartOutcome.ShowingStarted => Problem(statusCode: StatusCodes.Status409Conflict, title: "This showing has already started."),
            RushStartOutcome.AlreadyRunning => Problem(statusCode: StatusCodes.Status409Conflict, title: "A rush is already running on this showing."),
            RushStartOutcome.CoolingDown => Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Give it a few seconds before the next rush."),
            _ => throw new InvalidOperationException($"Unhandled rush outcome {result.Outcome}")
        };
    }

    [HttpGet("rush")]
    public ActionResult<RushStatusDto> Status(int eventId) => Ok(ToDto(rush.GetStatus(eventId)));

    [HttpDelete("rush")]
    public async Task<ActionResult> Stop(int eventId)
    {
        await rush.StopAsync(eventId);
        return NoContent();
    }

    [HttpPost("reset")]
    public async Task<ActionResult> Reset(int eventId, [FromServices] IShowingResetService reset, CancellationToken ct) =>
        await reset.ResetAsync(eventId, ct) ? NoContent() : NotFound();

    private static RushStatusDto ToDto(RushStatus s, long? queuePosition = null) =>
        new(s.Running, s.Mode?.ToString(), s.Bots, s.ActiveBots, s.SeatsBooked, s.StartedAt, queuePosition);
}
