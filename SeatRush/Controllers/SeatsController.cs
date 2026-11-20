using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Mvc;
using SeatRush.DTOs;

namespace SeatRush.Controllers;

public class SeatsController(ISeatService seats) : BaseApiController
{
    [HttpPost("{seatId:int}/hold")]
    public async Task<ActionResult<HoldDto>> Hold(int seatId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return MissingUser();

        var result = await seats.HoldSeatAsync(seatId, userId, ct);

        return result.Outcome switch
        {
            HoldOutcome.Held => Ok(new HoldDto(seatId, result.ExpiresAt!.Value)),
            HoldOutcome.SeatNotFound => NotFound(),
            HoldOutcome.SeatUnavailable => Problem(statusCode: StatusCodes.Status409Conflict, title: "Seat is no longer available."),
            HoldOutcome.HoldLimitReached => Problem(statusCode: StatusCodes.Status409Conflict, title: "You already hold the maximum number of seats."),
            _ => throw new InvalidOperationException($"Unhandled hold outcome {result.Outcome}")
        };
    }

    [HttpDelete("{seatId:int}/hold")]
    public async Task<ActionResult> Release(int seatId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return MissingUser();

        return await seats.ReleaseHoldAsync(seatId, userId, ct) ? NoContent() : NotFound();
    }
}
