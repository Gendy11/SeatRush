using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatRush.DTOs;

namespace SeatRush.Controllers;

/// <summary>
/// Anonymous guest identity (no signup/login). The access token goes back in the body and the client keeps it
/// in memory only; the refresh token goes in an HttpOnly cookie that JavaScript can never read.
/// </summary>
[AllowAnonymous]
public class AuthController(IAuthTokenService tokens, IWebHostEnvironment env) : BaseApiController
{
    public const string RefreshCookie = "seatrush_rt";

    /// <summary>
    /// Returns a fresh access token. With a valid refresh cookie: rotates it (same guest). Without one, or with
    /// an invalid/revoked one: starts a new guest. 409 = a parallel request just rotated the cookie; retry.
    /// </summary>
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

            // Invalid: fall through and start a new guest.
        }

        return Issue(await tokens.StartGuestAsync());
    }

    private ActionResult<AccessTokenDto> Issue(AuthTokens t)
    {
        Response.Cookies.Append(RefreshCookie, t.RefreshToken, new CookieOptions
        {
            HttpOnly = true,                              // JavaScript can't read it, so XSS can't steal it
            Secure = !env.IsDevelopment() || Request.IsHttps,
            SameSite = SameSiteMode.Strict,               // never sent on cross-site requests (CSRF)
            Path = "/api/auth",                           // only travels to the token endpoint, not every API call
            Expires = t.RefreshTokenExpiresAt
        });

        Response.Headers.CacheControl = "no-store";
        return Ok(new AccessTokenDto(t.AccessToken, t.AccessTokenExpiresAt));
    }
}
