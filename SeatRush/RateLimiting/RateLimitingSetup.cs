using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SeatRush.RateLimiting;

public static class RateLimitingSetup
{
    public const string Guest = "guest";

    public const string Holds = "holds";

    public const string Checkout = "checkout";

    public static IServiceCollection AddSeatRushRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = WriteRejectionAsync;

            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                ctx.Request.Path.StartsWithSegments("/hubs")
                    ? RateLimitPartition.GetNoLimiter("hubs")
                    : RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            o.AddPolicy(Guest, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));

            o.AddPolicy(Holds, ctx => RateLimitPartition.GetTokenBucketLimiter(UserOrIp(ctx), _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 20,
                TokensPerPeriod = 2,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1)
            }));

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

    private static string UserOrIp(HttpContext ctx) =>
        ctx.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value is { } sub ? $"u:{sub}" : $"ip:{ClientIp(ctx)}";

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
