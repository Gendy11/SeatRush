using Core.Entities;
using Core.Enums;
using IntegrationTests.Infrastructure;

namespace IntegrationTests;

[Collection(SeatRushCollection.Name)]
public class LiveUpdateTests(SeatRushFixture fx)
{
    [Fact]
    public async Task Hold_and_release_each_push_one_change_with_increasing_seq()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var user = Guid.NewGuid();
        var session = await fx.NewSessionService().GetOrStartAsync(eventId, user);

        await fx.NewHoldService().HoldAsync(seatIds[0], user);
        await fx.NewHoldService().ReleaseAsync(seatIds[0], user);

        var changes = fx.Notifier.For(eventId);
        Assert.Equal(2, changes.Count);

        Assert.Equal((seatIds[0], SeatStatus.Held), (changes[0].SeatId, changes[0].Status));
        Assert.Equal(session.ExpiresAt, changes[0].ExpiresAt!.Value, TimeSpan.FromMilliseconds(5));

        Assert.Equal((seatIds[0], SeatStatus.Available), (changes[1].SeatId, changes[1].Status));
        Assert.True(changes[1].Seq > changes[0].Seq);
    }

    [Fact]
    public async Task Failed_or_repeated_holds_push_nothing()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var (owner, other) = (Guid.NewGuid(), Guid.NewGuid());
        await fx.NewSessionService().GetOrStartAsync(eventId, owner);
        await fx.NewSessionService().GetOrStartAsync(eventId, other);

        await fx.NewHoldService().HoldAsync(seatIds[0], owner);
        await fx.NewHoldService().HoldAsync(seatIds[0], owner);   // idempotent re-hold
        await fx.NewHoldService().HoldAsync(seatIds[0], other);   // loses
        await fx.NewHoldService().ReleaseAsync(seatIds[0], other); // not the holder

        Assert.Single(fx.Notifier.For(eventId));
    }

    [Fact]
    public async Task Concurrent_changes_get_unique_seqs_and_one_push_per_winner()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync(rows: 2, seatsPerRow: 5); // 10 seats
        var users = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var u in users)
            await fx.NewSessionService().GetOrStartAsync(eventId, u);

        var start = new TaskCompletionSource();
        var attempts = users.SelectMany(u => seatIds.Select(s => (u, s)))
            .Select(async a => { await start.Task; await fx.NewHoldService().HoldAsync(a.s, a.u); })
            .ToArray();
        start.SetResult();
        await Task.WhenAll(attempts);

        var changes = fx.Notifier.For(eventId);
        Assert.Equal(seatIds.Length, changes.Count);                  // one push per seat actually won
        Assert.Equal(changes.Count, changes.Select(c => c.Seq).Distinct().Count()); // no two share a seq
    }

    [Fact]
    public async Task Changes_after_a_snapshot_have_a_higher_seq_than_the_snapshot()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var user = Guid.NewGuid();
        await fx.NewSessionService().GetOrStartAsync(eventId, user);

        var snapshot = await fx.NewSeatService().GetSeatMapAsync(eventId, user);
        await fx.NewHoldService().HoldAsync(seatIds[0], user);

        Assert.True(fx.Notifier.For(eventId).Single().Seq > snapshot.Seq);
    }

    [Fact]
    public async Task Seat_map_includes_hold_expiry_so_clients_can_free_seats_themselves()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var user = Guid.NewGuid();
        var session = await fx.NewSessionService().GetOrStartAsync(eventId, user);
        await fx.NewHoldService().HoldAsync(seatIds[0], user);

        var seat = (await fx.NewSeatService().GetSeatMapAsync(eventId, viewerId: null)).Seats.Single(s => s.Id == seatIds[0]);

        Assert.Equal(SeatStatus.Held, seat.Status);
        Assert.Equal(session.ExpiresAt, seat.HoldExpiresAt!.Value, TimeSpan.FromMilliseconds(5));
    }
}
