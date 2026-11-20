using IntegrationTests.Infrastructure;

namespace IntegrationTests;

[Collection(SeatRushCollection.Name)]
public class BookingSessionTests(SeatRushFixture fx)
{
    [Fact]
    public async Task Reopening_returns_the_same_deadline()
    {
        var (eventId, _) = await fx.CreateShowingAsync();
        var user = Guid.NewGuid();

        var first = await fx.NewSessionService().GetOrStartAsync(eventId, user);
        await Task.Delay(200);
        var second = await fx.NewSessionService().GetOrStartAsync(eventId, user);

        Assert.Equal(first.ExpiresAt, second.ExpiresAt, TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task Parallel_opens_create_a_single_session()
    {
        var (eventId, _) = await fx.CreateShowingAsync();
        var user = Guid.NewGuid();

        var sessions = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => fx.NewSessionService().GetOrStartAsync(eventId, user)));

        Assert.Single(sessions.Select(s => s.ExpiresAt.Ticks / TimeSpan.TicksPerMillisecond).Distinct());
    }

    [Fact]
    public async Task Each_showing_gets_its_own_session()
    {
        var (eventA, _) = await fx.CreateShowingAsync();
        var (eventB, _) = await fx.CreateShowingAsync();
        var user = Guid.NewGuid();

        await fx.NewSessionService().GetOrStartAsync(eventA, user);

        Assert.NotNull(await fx.NewSessionService().GetActiveAsync(eventA, user));
        Assert.Null(await fx.NewSessionService().GetActiveAsync(eventB, user));
    }
}
