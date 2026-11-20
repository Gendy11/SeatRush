using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace SeatRush.Middleware;

/// <summary>
/// Redis down → 503 "try again" instead of a 500. Sessions and holds fail closed;
/// bookings in Postgres are unaffected.
/// </summary>
public class RedisUnavailableExceptionHandler(ILogger<RedisUnavailableExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not (RedisConnectionException or RedisTimeoutException))
            return false;

        logger.LogWarning(exception, "Redis unavailable");

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Seat selection is temporarily unavailable. Please try again."
        }, ct);

        return true;
    }
}
