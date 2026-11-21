using Core.Models;
using Infrastructure.Services;
using IntegrationTests.Infrastructure;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace IntegrationTests;

[Collection(SeatRushCollection.Name)]
public class AuthTokenTests(SeatRushFixture fx)
{
    private static readonly AuthOptions Options = new() { SigningKey = "test-signing-key-at-least-32-characters-long!" };

    private AuthTokenService NewService(TimeProvider clock) => new(fx.Redis, Microsoft.Extensions.Options.Options.Create(Options), clock);

    [Fact]
    public async Task New_guest_gets_a_signed_access_token_carrying_its_user_id()
    {
        var tokens = await NewService(TimeProvider.System).StartGuestAsync();

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(tokens.AccessToken, new()
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = Options.CreateSigningKey()
        });

        Assert.True(validation.IsValid);
        Assert.Equal(tokens.UserId.ToString(), validation.Claims[JwtRegisteredClaimNames.Sub]);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_keeps_the_same_user()
    {
        var service = NewService(TimeProvider.System);
        var first = await service.StartGuestAsync();

        var refreshed = await service.RefreshAsync(first.RefreshToken);

        Assert.Equal(RefreshOutcome.Issued, refreshed.Outcome);
        Assert.Equal(first.UserId, refreshed.Tokens!.UserId);
        Assert.NotEqual(first.RefreshToken, refreshed.Tokens.RefreshToken);
    }

    [Fact]
    public async Task Replaying_a_used_token_after_the_grace_window_revokes_the_whole_family()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = NewService(clock);
        var first = await service.StartGuestAsync();
        var second = (await service.RefreshAsync(first.RefreshToken)).Tokens!;   // legit rotation

        clock.Advance(TimeSpan.FromMinutes(1));
        var replay = await service.RefreshAsync(first.RefreshToken);           // attacker replays the old one

        Assert.Equal(RefreshOutcome.Invalid, replay.Outcome);
        // The legit user's newer token is now dead too: the whole chain is revoked.
        Assert.Equal(RefreshOutcome.Invalid, (await service.RefreshAsync(second.RefreshToken)).Outcome);
    }

    [Fact]
    public async Task Reuse_within_the_grace_window_asks_to_retry_instead_of_revoking()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = NewService(clock);
        var first = await service.StartGuestAsync();
        var second = (await service.RefreshAsync(first.RefreshToken)).Tokens!;

        clock.Advance(TimeSpan.FromSeconds(2));                                // second tab, same moment
        var parallel = await service.RefreshAsync(first.RefreshToken);

        Assert.Equal(RefreshOutcome.RetryLater, parallel.Outcome);
        Assert.Equal(RefreshOutcome.Issued, (await service.RefreshAsync(second.RefreshToken)).Outcome);
    }

    [Fact]
    public async Task Simultaneous_refreshes_with_one_token_issue_exactly_one_new_pair()
    {
        var service = NewService(TimeProvider.System);
        var first = await service.StartGuestAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => service.RefreshAsync(first.RefreshToken)));

        Assert.Single(results, r => r.Outcome == RefreshOutcome.Issued);
        Assert.All(results.Where(r => r.Outcome != RefreshOutcome.Issued), r => Assert.Equal(RefreshOutcome.RetryLater, r.Outcome));
    }

    [Fact]
    public async Task Unknown_tokens_are_rejected_and_raw_tokens_are_never_stored()
    {
        var service = NewService(TimeProvider.System);
        var tokens = await service.StartGuestAsync();

        Assert.Equal(RefreshOutcome.Invalid, (await service.RefreshAsync("made-up-token")).Outcome);

        // Redis holds the SHA-256 of the token, not the token itself.
        var server = fx.Redis.GetServer(fx.Redis.GetEndPoints()[0]);
        var keys = server.Keys(pattern: "rt:*").Select(k => k.ToString()).ToList();
        Assert.DoesNotContain(keys, k => k.Contains(tokens.RefreshToken));
    }
}
