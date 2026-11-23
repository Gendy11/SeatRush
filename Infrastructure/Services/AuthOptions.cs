using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

public class AuthOptions
{
    public const string SectionName = "Auth";

    public string SigningKey { get; set; } = "";

    public string Issuer { get; set; } = "seatrush";
    public string Audience { get; set; } = "seatrush-client";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan RefreshReuseGrace { get; set; } = TimeSpan.FromSeconds(10);

    public SymmetricSecurityKey CreateSigningKey()
    {
        if (SigningKey.Length < 32)
            throw new InvalidOperationException("Auth:SigningKey must be at least 32 characters.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    }
}
