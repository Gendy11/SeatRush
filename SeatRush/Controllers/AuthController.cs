using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatRush.RateLimiting;
using SeatRush.DTOs;

namespace SeatRush.Controllers;

[AllowAnonymous]
[EnableRateLimiting(RateLimitingSetup.Guest)]
public class AuthController(IAuthTokenService tokens, IWebHostEnvironment env) : BaseApiController
{
    public const string RefreshCookie = "seatrush_rt";

    [HttpPost("token")]
    public async Task<ActionResult<AccessTokenDto>> Token()
    {
        if (Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
        {
            var result = await tokens.RefreshAsync(refreshToken);

            if (result.Outcome == RefreshOutcome.Issued)
                return Issue(result.Tokens!);

            if (result.Outcome == RefreshOutcome.RetryLater)
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "Token is being refreshed, retry.");
        }

        return Issue(await tokens.StartGuestAsync());
    }

    private ActionResult<AccessTokenDto> Issue(AuthTokens t)
    {
        Response.Cookies.Append(RefreshCookie, t.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment() || Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = t.RefreshTokenExpiresAt
        });

        Response.Headers.CacheControl = "no-store";
        return Ok(new AccessTokenDto(t.AccessToken, t.AccessTokenExpiresAt));
    }
}
