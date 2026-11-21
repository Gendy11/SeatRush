using Core.Models;

namespace Core.Interfaces;

/// <summary>Anonymous guest identity: no signup or login, the server creates and signs the user id.</summary>
public interface IAuthTokenService
{
    /// <summary>Creates a new guest (random user id) and issues its first token pair.</summary>
    Task<AuthTokens> StartGuestAsync();

    /// <summary>
    /// Exchanges a refresh token for a new token pair (rotation: the old refresh token stops working).
    /// Replaying an already-used token revokes its whole family.
    /// </summary>
    Task<RefreshResult> RefreshAsync(string refreshToken);
}
