using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatRush.RateLimiting;
using SeatRush.DTOs;

namespace SeatRush.Controllers;

[Authorize]
[EnableRateLimiting(RateLimitingSetup.Holds)]
public class SeatsController(ISeatHoldService holds) : BaseApiController
{
    [HttpPost("{seatId:int}/hold")]
    public async Task<ActionResult<HoldDto>> Hold(int seatId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return MissingUser();

        var result = await holds.HoldAsync(seatId, userId, ct);

        return result.Outcome switch
        {
            HoldOutcome.Held => Ok(new HoldDto(seatId, result.ExpiresAt!.Value)),
            HoldOutcome.SeatNotFound => NotFound(),
            HoldOutcome.SeatUnavailable => Problem(statusCode: StatusCodes.Status409Conflict, title: "Seat is no longer available."),
            HoldOutcome.NoActiveSession => Problem(statusCode: StatusCodes.Status409Conflict, title: "Your booking session has expired."),
            _ => throw new InvalidOperationException($"Unhandled hold outcome {result.Outcome}")
        };
    }

    [HttpDelete("{seatId:int}/hold")]
    public async Task<ActionResult> Release(int seatId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return MissingUser();

        return await holds.ReleaseAsync(seatId, userId, ct) ? NoContent() : NotFound();
    }
}
