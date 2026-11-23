using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace Infrastructure.Services;

public class AuthTokenService(IConnectionMultiplexer redis, IOptions<AuthOptions> options, TimeProvider clock) : IAuthTokenService
{
    private readonly IDatabase _redis = redis.GetDatabase();
    private readonly AuthOptions _options = options.Value;
    private readonly JsonWebTokenHandler _jwt = new();

    public async Task<AuthTokens> StartGuestAsync()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid().ToString("N");
        return await IssueAsync(userId, familyId);
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken)
    {
        var key = RedisKeys.RefreshToken(Hash(refreshToken));
        var entry = (await _redis.HashGetAllAsync(key)).ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

        if (!entry.TryGetValue("user", out var user) || !Guid.TryParse(user, out var userId) ||
            !entry.TryGetValue("family", out var familyId) || !entry.TryGetValue("usedAt", out var usedAtRaw))
            return new RefreshResult(RefreshOutcome.Invalid);

        if (await _redis.KeyExistsAsync(RedisKeys.RefreshFamily(familyId)))
            return new RefreshResult(RefreshOutcome.Invalid);

        var now = clock.GetUtcNow();
        var usedAt = long.Parse(usedAtRaw);

        if (usedAt != 0)
        {
            if (now - DateTimeOffset.FromUnixTimeMilliseconds(usedAt) < _options.RefreshReuseGrace)
                return new RefreshResult(RefreshOutcome.RetryLater);

            await RevokeFamilyAsync(familyId);
            return new RefreshResult(RefreshOutcome.Invalid);
        }

        var tx = _redis.CreateTransaction();
        tx.AddCondition(Condition.HashEqual(key, "usedAt", "0"));
        _ = tx.HashSetAsync(key, "usedAt", now.ToUnixTimeMilliseconds());
        if (!await tx.ExecuteAsync())
            return new RefreshResult(RefreshOutcome.RetryLater);

        return new RefreshResult(RefreshOutcome.Issued, await IssueAsync(userId, familyId));
    }

    private async Task<AuthTokens> IssueAsync(Guid userId, string familyId)
    {
        var now = clock.GetUtcNow();
        var accessExpires = now.Add(_options.AccessTokenLifetime);
        var refreshExpires = now.Add(_options.RefreshTokenLifetime);

        var accessToken = _jwt.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ]),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessExpires.UtcDateTime,
            SigningCredentials = new SigningCredentials(_options.CreateSigningKey(), SecurityAlgorithms.HmacSha256)
        });

        var refreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var key = RedisKeys.RefreshToken(Hash(refreshToken));

        var tx = _redis.CreateTransaction();
        _ = tx.HashSetAsync(key, [
            new HashEntry("user", userId.ToString()),
            new HashEntry("family", familyId),
            new HashEntry("usedAt", 0)
        ]);
        _ = tx.KeyExpireAsync(key, _options.RefreshTokenLifetime);
        await tx.ExecuteAsync();

        return new AuthTokens(userId, accessToken, accessExpires.UtcDateTime, refreshToken, refreshExpires.UtcDateTime);
    }

    private Task RevokeFamilyAsync(string familyId) =>
        _redis.StringSetAsync(RedisKeys.RefreshFamily(familyId), "revoked", _options.RefreshTokenLifetime);

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
