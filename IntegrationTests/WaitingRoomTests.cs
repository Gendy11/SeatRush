using Core.Models;
using Infrastructure.Services;
using IntegrationTests.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace IntegrationTests;

[Collection(SeatRushCollection.Name)]
public class WaitingRoomTests(SeatRushFixture fx)
{
    private WaitingRoomService NewRoom(int capacity, TimeProvider? clock = null) =>
        new(fx.Redis,
            Options.Create(new WaitingRoomOptions { Capacity = capacity }),
            clock ?? TimeProvider.System);

    [Fact]
    public async Task No_crowd_lets_you_straight_in()
    {
        var (eventId, _) = await fx.CreateShowingAsync();

        var status = await NewRoom(capacity: 2).JoinAsync(eventId, Guid.NewGuid());

        Assert.Equal(QueueState.Admitted, status.State);
    }

    [Fact]
    public async Task Full_room_puts_people_in_line_in_arrival_order()
    {
        var (eventId, _) = await fx.CreateShowingAsync();
        var room = NewRoom(capacity: 1);

        await room.JoinAsync(eventId, Guid.NewGuid()); // takes the only place
        var second = await room.JoinAsync(eventId, Guid.NewGuid());
        var third = await room.JoinAsync(eventId, Guid.NewGuid());

        Assert.Equal((QueueState.Waiting, 1L), (second.State, second.Position));
        Assert.Equal((QueueState.Waiting, 2L), (third.State, third.Position));
    }

    [Fact]
    public async Task Joining_again_keeps_your_place()
    {
        var (eventId, _) = await fx.CreateShowingAsync();
        var room = NewRoom(capacity: 1);
        var me = Guid.NewGuid();

        await room.JoinAsync(eventId, Guid.NewGuid());
        await room.JoinAsync(eventId, me);
        await room.JoinAsync(eventId, Guid.NewGuid());
        var again = await room.JoinAsync(eventId, me); // refresh / second tab

        Assert.Equal(1L, again.Position);
    }

    [Fact]
    public async Task Leaving_lets_the_next_person_in()
    {
        var (eventId, _) = await fx.CreateShowingAsync();
        var room = NewRoom(capacity: 1);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await room.JoinAsync(eventId, first);
        await room.JoinAsync(eventId, second);

        await room.LeaveAsync(eventId, first); // e.g. checked out
        await room.AdmitAsync(eventId);

        Assert.Equal(QueueState.Admitted, (await room.GetStatusAsync(eventId, second)).State);
    }

    [Fact]
    public async Task Unused_pass_expires_and_goes_to_the_next_person()
    {
        var (eventId, _) = await fx.CreateShowingAsync();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var room = NewRoom(capacity: 1, clock);
        var noShow = Guid.NewGuid();
        var next = Guid.NewGuid();

        await room.JoinAsync(eventId, noShow); // admitted, never opens the seat map
        await room.JoinAsync(eventId, next);

        // 61s pass; `next` keeps checking their place every few seconds, like the browser does.
        for (var i = 0; i < 61; i += 3)
        {
            clock.Advance(TimeSpan.FromSeconds(3));
            await room.GetStatusAsync(eventId, next);
        }

        await room.AdmitAsync(eventId);

        Assert.False(await room.IsAdmittedAsync(eventId, noShow));
        Assert.True(await room.IsAdmittedAsync(eventId, next));

        // The no-show comes back later: not punished, first in line again.
        await room.JoinAsync(eventId, Guid.NewGuid());
        var back = await room.GetStatusAsync(eventId, noShow);
        Assert.Equal((QueueState.Waiting, 1L), (back.State, back.Position));
    }

    [Fact]
    public async Task Silent_people_are_skipped_but_keep_their_place()
    {
        var (eventId, _) = await fx.CreateShowingAsync();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var room = NewRoom(capacity: 1, clock);
        var inside = Guid.NewGuid();
        var closedTab = Guid.NewGuid();
        var stillHere = Guid.NewGuid();

        await room.JoinAsync(eventId, inside);
        await room.JoinAsync(eventId, closedTab);
        await room.JoinAsync(eventId, stillHere);

        // closedTab goes silent (hidden tab / locked phone); stillHere keeps checking every few seconds.
        for (var i = 0; i < 30; i += 3)
        {
            clock.Advance(TimeSpan.FromSeconds(3));
            await room.GetStatusAsync(eventId, stillHere);
        }
        await room.LeaveAsync(eventId, inside);
        await room.AdmitAsync(eventId);

        Assert.True(await room.IsAdmittedAsync(eventId, stillHere));
        Assert.False(await room.IsAdmittedAsync(eventId, closedTab));
    }

    [Fact]
    public async Task Skipped_person_who_comes_back_is_first_in_line()
    {
        var (eventId, _) = await fx.CreateShowingAsync();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var room = NewRoom(capacity: 1, clock);
        var inside = Guid.NewGuid();
        var lockedPhone = Guid.NewGuid();
        var later = Guid.NewGuid();

        await room.JoinAsync(eventId, inside);
        await room.JoinAsync(eventId, lockedPhone);
        clock.Advance(TimeSpan.FromSeconds(30)); // phone locked: silent
        await room.JoinAsync(eventId, later);

        // lockedPhone's turn comes while silent: skipped, so the place goes to `later` instead of being wasted.
        await room.LeaveAsync(eventId, inside);
        await room.AdmitAsync(eventId);
        Assert.True(await room.IsAdmittedAsync(eventId, later));

        // Phone unlocked: the next poll puts them back with their old ticket, ahead of anyone who joined after.
        await room.JoinAsync(eventId, Guid.NewGuid());
        var back = await room.GetStatusAsync(eventId, lockedPhone);

        Assert.Equal((QueueState.Waiting, 1L), (back.State, back.Position));
    }

    [Fact]
    public async Task Parallel_rush_never_lets_in_more_than_capacity()
    {
        var (eventId, _) = await fx.CreateShowingAsync();
        const int capacity = 10;
        var users = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToList();

        // 200 joins at once, each from its own "instance", plus admitters running alongside.
        await Task.WhenAll(users
            .Select(u => (Task)NewRoom(capacity).JoinAsync(eventId, u))
            .Concat(Enumerable.Range(0, 20).Select(_ => (Task)NewRoom(capacity).AdmitAsync(eventId))));

        var statuses = await Task.WhenAll(users.Select(u => NewRoom(capacity).GetStatusAsync(eventId, u)));

        Assert.True(statuses.Count(s => s.State == QueueState.Admitted) <= capacity);
        Assert.Equal(users.Count, statuses.Count(s => s.State is QueueState.Admitted or QueueState.Waiting));
        Assert.Equal(
            Enumerable.Range(1, statuses.Count(s => s.State == QueueState.Waiting)).Select(i => (long)i),
            statuses.Where(s => s.State == QueueState.Waiting).Select(s => s.Position!.Value).Order());
    }
}
