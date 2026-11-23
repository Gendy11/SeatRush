using Core.Models;

namespace Core.Interfaces;

public interface IAuthTokenService
{
    Task<AuthTokens> StartGuestAsync();

    Task<RefreshResult> RefreshAsync(string refreshToken);
}
