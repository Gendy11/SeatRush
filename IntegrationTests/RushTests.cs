using Core.Enums;
using Core.Interfaces;
using Core.Models;
using Infrastructure;
using Infrastructure.Demo;
using Infrastructure.Services;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace IntegrationTests;

[Collection(SeatRushCollection.Name)]
public class RushTests(SeatRushFixture fx)
{
    /// <summary>The real service registrations, against the test containers, with a fast-forwarded demo.</summary>
    private ServiceProvider BuildServices(int roomCapacity = 50, Action<RushOptions>? rush = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fx.ConnectionString,
            ["ConnectionStrings:Redis"] = fx.Redis.Configuration,
            ["WaitingRoom:Capacity"] = roomCapacity.ToString()
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        services.AddSingleton<ISeatMapNotifier>(fx.Notifier);
        services.Configure<RushOptions>(o =>
        {
            o.NormalArrivalMin = o.NormalArrivalMax = TimeSpan.FromMilliseconds(50);
            o.ExtremeArrivalWindow = TimeSpan.FromMilliseconds(200);
            o.PollInterval = TimeSpan.FromMilliseconds(50);
            o.LookMin = o.LookMax = TimeSpan.FromMilliseconds(10);
            o.ClickGapMin = o.ClickGapMax = TimeSpan.FromMilliseconds(5);
            o.DecideMin = TimeSpan.FromMilliseconds(20);
            o.DecideMax = TimeSpan.FromMilliseconds(80);
            o.MaxDuration = TimeSpan.FromSeconds(20);
            o.Cooldown = TimeSpan.Zero;
            rush?.Invoke(o);
        });
        return services.BuildServiceProvider();
    }

    private static async Task WaitUntilFinishedAsync(IRushSimulator sim, int eventId, Func<Task>? tick = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (sim.GetStatus(eventId).Running)
        {
            Assert.True(DateTime.UtcNow < deadline, "Rush didn't finish in time.");
            if (tick is not null) await tick();
            await Task.Delay(50);
        }
    }

    [Fact]
    public async Task Normal_rush_sells_the_hall_through_the_real_booking_path()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync(rows: 2, seatsPerRow: 5);
        await using var sp = BuildServices(rush: o => o.NormalBookChance = 1.0);
        var sim = sp.GetRequiredService<IRushSimulator>();

        var started = await sim.StartAsync(eventId, Guid.NewGuid());
        Assert.Equal((RushStartOutcome.Started, RushMode.Normal), (started.Outcome, started.Mode));
        Assert.Equal(RushStartOutcome.AlreadyRunning, (await sim.StartAsync(eventId, Guid.NewGuid())).Outcome);

        await WaitUntilFinishedAsync(sim, eventId); // stops by itself once sold out

        await using var db = fx.NewDbContext();
        var booked = await db.Seats.Where(s => s.EventId == eventId && s.Status == SeatStatus.Booked).Select(s => s.Id).ToListAsync();
        var items = await db.Bookings.Where(b => b.EventId == eventId).SelectMany(b => b.Items).Select(i => i.SeatId).ToListAsync();
        var buyers = await db.Bookings.Where(b => b.EventId == eventId).Select(b => b.UserId).ToListAsync();

        Assert.Equal(seatIds.Order(), booked.Order());   // sold out
        Assert.Equal(booked.Order(), items.Order());      // every booked seat in exactly one booking
        Assert.All(buyers, b => Assert.True(RushBots.IsBot(b)));
        Assert.Empty(await fx.NewHoldService().GetActiveHoldsAsync(eventId)); // nothing left held
    }

    [Fact]
    public async Task Extreme_rush_puts_the_starter_in_line_behind_the_crowd()
    {
        var (eventId, _) = await fx.CreateShowingAsync(rows: 4, seatsPerRow: 6, hasWaitingRoom: true);
        await using var sp = BuildServices(roomCapacity: 5, rush: o =>
        {
            o.ExtremeBots = 40;
            o.StarterJoinsAfter = 20;
        });
        var sim = sp.GetRequiredService<IRushSimulator>();
        var room = sp.GetRequiredService<IWaitingRoomService>();
        var me = Guid.NewGuid();

        // I was inside choosing seats when I pressed the button.
        await room.JoinAsync(eventId, me);

        var started = await sim.StartAsync(eventId, me);

        Assert.Equal(RushMode.Extreme, started.Mode);
        Assert.Equal(20 - 5 + 1, started.QueuePosition); // 20 bots ahead, 5 of them already let in
        Assert.Equal(QueueState.Waiting, (await room.GetStatusAsync(eventId, me)).State);

        // The admitter (a background service in the API) lets people in as bots finish.
        await WaitUntilFinishedAsync(sim, eventId, () => room.AdmitAsync(eventId));

        var status = sim.GetStatus(eventId);
        Assert.False(status.Running);

        await using var db = fx.NewDbContext();
        Assert.True(await db.Bookings.AnyAsync(b => b.EventId == eventId));
        Assert.Empty(await fx.NewHoldService().GetActiveHoldsAsync(eventId));
    }

    [Fact]
    public async Task Reset_removes_bot_bookings_only_and_clears_the_showing()
    {
        var (eventId, seats) = await fx.CreateShowingAsync(hasWaitingRoom: true);
        var human = Guid.NewGuid();
        var bot = RushBots.Id(42);
        Assert.True(RushBots.IsBot(bot));
        Assert.False(RushBots.IsBot(human));

        // A real booking, a bot booking, a live hold and someone in line.
        await fx.HoldSeatsAsync(eventId, human, seats[0]);
        await fx.NewBookingService().CheckoutAsync(eventId, human, [seats[0]], PaymentMethod.PayAtCounter, "h1");
        await fx.HoldSeatsAsync(eventId, bot, seats[1]);
        await fx.NewBookingService().CheckoutAsync(eventId, bot, [seats[1]], PaymentMethod.PayAtCounter, "b1");
        await fx.HoldSeatsAsync(eventId, Guid.NewGuid(), seats[2]);

        await using var sp = BuildServices(roomCapacity: 1);
        await sp.GetRequiredService<IWaitingRoomService>().JoinAsync(eventId, Guid.NewGuid());
        await sp.GetRequiredService<IWaitingRoomService>().JoinAsync(eventId, Guid.NewGuid());
        var seqBefore = await fx.NewHoldService().GetSeqAsync(eventId);

        using (var scope = sp.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<IShowingResetService>().ResetAsync(eventId));

        await using var db = fx.NewDbContext();
        var status = await db.Seats.Where(s => s.EventId == eventId).ToDictionaryAsync(s => s.Id, s => s.Status);
        Assert.Equal(SeatStatus.Booked, status[seats[0]]);    // the human keeps their ticket
        Assert.Equal(SeatStatus.Available, status[seats[1]]); // the bot's seat is free again
        Assert.Equal([human], await db.Bookings.Where(b => b.EventId == eventId).Select(b => b.UserId).ToListAsync());

        Assert.Empty(await fx.NewHoldService().GetActiveHoldsAsync(eventId));
        var redis = fx.Redis.GetDatabase();
        Assert.False(await redis.KeyExistsAsync(RedisKeys.Queue(eventId)));
        Assert.False(await redis.KeyExistsAsync(RedisKeys.Room(eventId)));
        Assert.True(await fx.NewHoldService().GetSeqAsync(eventId) >= seqBefore); // never goes backwards
        Assert.Contains(eventId, fx.Notifier.Resets);
    }
}
