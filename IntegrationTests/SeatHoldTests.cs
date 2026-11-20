using Core.Entities;
using Core.Models;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

[Collection(SeatRushCollection.Name)]
public class SeatHoldTests(SeatRushFixture fx)
{
    [Fact]
    public async Task Many_users_grabbing_the_same_seat_at_once_exactly_one_wins()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var seatId = seatIds[0];
        const int users = 50;

        var userIds = Enumerable.Range(0, users).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var u in userIds)
            await fx.NewSessionService().GetOrStartAsync(eventId, u);

        // Release all requests at the same instant.
        var start = new TaskCompletionSource();
        var attempts = userIds.Select(async u =>
        {
            await start.Task;
            return (User: u, Result: await fx.NewHoldService().HoldAsync(seatId, u));
        }).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(attempts);

        var winners = results.Where(r => r.Result.Outcome == HoldOutcome.Held).ToList();
        Assert.Single(winners);
        Assert.Equal(users - 1, results.Count(r => r.Result.Outcome == HoldOutcome.SeatUnavailable));

        var holds = await fx.NewHoldService().GetActiveHoldsAsync(eventId);
        Assert.Equal(winners[0].User, holds[seatId].HolderId);
    }

    [Fact]
    public async Task Many_users_racing_for_many_seats_never_share_a_seat()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync(rows: 4, seatsPerRow: 5); // 20 seats
        var userIds = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var u in userIds)
            await fx.NewSessionService().GetOrStartAsync(eventId, u);

        // Every user tries every seat, all at once.
        var start = new TaskCompletionSource();
        var attempts = userIds
            .SelectMany(u => seatIds.Select(s => (User: u, Seat: s)))
            .Select(async a =>
            {
                await start.Task;
                var r = await fx.NewHoldService().HoldAsync(a.Seat, a.User);
                return (a.User, a.Seat, r.Outcome);
            })
            .ToArray();
        start.SetResult();
        var results = await Task.WhenAll(attempts);

        // Each seat was won by exactly one user (idempotent re-holds by the same winner are fine).
        foreach (var seat in seatIds)
        {
            var winners = results.Where(r => r.Seat == seat && r.Outcome == HoldOutcome.Held).Select(r => r.User).Distinct();
            Assert.Single(winners);
        }
    }

    [Fact]
    public async Task Holding_your_own_seat_again_succeeds()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var user = Guid.NewGuid();
        var session = await fx.NewSessionService().GetOrStartAsync(eventId, user);

        var first = await fx.NewHoldService().HoldAsync(seatIds[0], user);
        var second = await fx.NewHoldService().HoldAsync(seatIds[0], user);

        Assert.Equal(HoldOutcome.Held, first.Outcome);
        Assert.Equal(HoldOutcome.Held, second.Outcome);
        Assert.Equal(session.ExpiresAt, second.ExpiresAt!.Value, TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task Only_the_holder_can_release_and_then_the_seat_is_free_again()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var (owner, other) = (Guid.NewGuid(), Guid.NewGuid());
        await fx.NewSessionService().GetOrStartAsync(eventId, owner);
        await fx.NewSessionService().GetOrStartAsync(eventId, other);

        await fx.NewHoldService().HoldAsync(seatIds[0], owner);

        Assert.False(await fx.NewHoldService().ReleaseAsync(seatIds[0], other));
        Assert.True(await fx.NewHoldService().ReleaseAsync(seatIds[0], owner));

        var retake = await fx.NewHoldService().HoldAsync(seatIds[0], other);
        Assert.Equal(HoldOutcome.Held, retake.Outcome);
    }

    [Fact]
    public async Task Cannot_hold_without_an_active_session()
    {
        var (_, seatIds) = await fx.CreateShowingAsync();

        var result = await fx.NewHoldService().HoldAsync(seatIds[0], Guid.NewGuid());

        Assert.Equal(HoldOutcome.NoActiveSession, result.Outcome);
    }

    [Fact]
    public async Task Holds_expire_when_the_session_ends_and_another_user_can_take_the_seat()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var shortSessions = fx.NewSessionService(TimeSpan.FromSeconds(2));
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());

        await shortSessions.GetOrStartAsync(eventId, first);
        Assert.Equal(HoldOutcome.Held, (await fx.NewHoldService(shortSessions).HoldAsync(seatIds[0], first)).Outcome);

        await Task.Delay(TimeSpan.FromSeconds(2.5));

        Assert.Empty(await fx.NewHoldService().GetActiveHoldsAsync(eventId));

        await fx.NewSessionService().GetOrStartAsync(eventId, second);
        Assert.Equal(HoldOutcome.Held, (await fx.NewHoldService().HoldAsync(seatIds[0], second)).Outcome);
    }

    [Fact]
    public async Task Booked_seats_cannot_be_held()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        await using (var db = fx.NewDbContext())
        {
            await db.Seats.Where(s => s.Id == seatIds[0])
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, SeatStatus.Booked));
        }

        var user = Guid.NewGuid();
        await fx.NewSessionService().GetOrStartAsync(eventId, user);

        Assert.Equal(HoldOutcome.SeatUnavailable, (await fx.NewHoldService().HoldAsync(seatIds[0], user)).Outcome);
    }

    [Fact]
    public async Task Seat_map_shows_holds_and_flags_the_viewers_own_seats()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var (me, someoneElse) = (Guid.NewGuid(), Guid.NewGuid());
        await fx.NewSessionService().GetOrStartAsync(eventId, me);
        await fx.NewSessionService().GetOrStartAsync(eventId, someoneElse);

        await fx.NewHoldService().HoldAsync(seatIds[0], me);
        await fx.NewHoldService().HoldAsync(seatIds[1], someoneElse);

        var map = (await fx.NewSeatService().GetSeatMapAsync(eventId, me)).Seats.ToDictionary(s => s.Id);

        Assert.Equal((SeatStatus.Held, true), (map[seatIds[0]].Status, map[seatIds[0]].IsMine));
        Assert.Equal((SeatStatus.Held, false), (map[seatIds[1]].Status, map[seatIds[1]].IsMine));
        Assert.Equal((SeatStatus.Available, false), (map[seatIds[2]].Status, map[seatIds[2]].IsMine));
    }
}
