namespace Core.Models;

/// <summary>
/// A guest's credentials. The access token is short-lived and sent on every API call; the refresh token
/// is long-lived, stored in an HttpOnly cookie, and only used to get new access tokens.
/// </summary>
public record AuthTokens(
    Guid UserId,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

public enum RefreshOutcome
{
    Issued,

    /// <summary>
    /// The token was used a moment ago by a parallel request (e.g. two tabs refreshing at once).
    /// Not theft: the client should retry, because the browser now holds the newer cookie.
    /// </summary>
    RetryLater,

    /// <summary>Unknown, expired, revoked, or a stolen token was replayed (its whole family is now revoked).</summary>
    Invalid
}

public record RefreshResult(RefreshOutcome Outcome, AuthTokens? Tokens = null);
