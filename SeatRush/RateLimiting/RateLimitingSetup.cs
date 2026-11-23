using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SeatRush.RateLimiting;

/// <summary>
/// FR20: per-client request limits, using ASP.NET's built-in in-memory rate limiter.
/// Counters live in this process, so with several API instances each one counts separately
/// (fine for a single-instance deployment; a shared Redis counter or the CDN/WAF would be the next step).
/// </summary>
public static class RateLimitingSetup
{
    /// <summary>New guests and token refreshes. Strict: stops one machine minting thousands of guest ids.</summary>
    public const string Guest = "guest";

    /// <summary>Holding and releasing seats. Allows fast clicking, blocks scripted seat grabbing.</summary>
    public const string Holds = "holds";

    /// <summary>Creating bookings.</summary>
    public const string Checkout = "checkout";

    public static IServiceCollection AddSeatRushRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = WriteRejectionAsync;

            // Every request, per IP: a high ceiling that only a flood would hit. SignalR is exempt
            // (one long-lived connection, plus reconnects shouldn't be refused).
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                ctx.Request.Path.StartsWithSegments("/hubs")
                    ? RateLimitPartition.GetNoLimiter("hubs")
                    : RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            // Per IP: the caller has no identity yet when asking for one.
            o.AddPolicy(Guest, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));

            // Per user: token bucket = bursts of 20 clicks, then 2 per second sustained.
            o.AddPolicy(Holds, ctx => RateLimitPartition.GetTokenBucketLimiter(UserOrIp(ctx), _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 20,
                TokensPerPeriod = 2,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1)
            }));

            // Per user: a real person checks out once or twice; retries are idempotent anyway.
            o.AddPolicy(Checkout, ctx => RateLimitPartition.GetFixedWindowLimiter(UserOrIp(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
        });

        return services;
    }

    private static string ClientIp(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>The signed-in guest's id when there is one (runs after authentication), else the IP.</summary>
    private static string UserOrIp(HttpContext ctx) =>
        ctx.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value is { } sub ? $"u:{sub}" : $"ip:{ClientIp(ctx)}";

    /// <summary>429 as ProblemDetails, with Retry-After so clients know when to try again.</summary>
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken ct)
    {
        var http = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests. Slow down and try again shortly." }
        });
    }
}
