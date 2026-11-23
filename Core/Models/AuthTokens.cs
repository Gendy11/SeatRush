namespace Core.Models;

public record AuthTokens(
    Guid UserId,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

public enum RefreshOutcome
{
    Issued,

    RetryLater,

    Invalid
}

public record RefreshResult(RefreshOutcome Outcome, AuthTokens? Tokens = null);
