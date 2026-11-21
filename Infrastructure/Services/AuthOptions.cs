using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>HMAC key for signing access tokens (≥ 32 chars). Production: set via the Auth__SigningKey env var.</summary>
    public string SigningKey { get; set; } = "";

    public string Issuer { get; set; } = "seatrush";
    public string Audience { get; set; } = "seatrush-client";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>A used refresh token replayed within this window is treated as a parallel request, not theft.</summary>
    public TimeSpan RefreshReuseGrace { get; set; } = TimeSpan.FromSeconds(10);

    public SymmetricSecurityKey CreateSigningKey()
    {
        if (SigningKey.Length < 32)
            throw new InvalidOperationException("Auth:SigningKey must be at least 32 characters.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    }
}
