using Core.Entities;
using Core.Enums;
using Core.Models;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

[Collection(SeatRushCollection.Name)]
public class BookingTests(SeatRushFixture fx)
{
    private static string NewKey() => Guid.NewGuid().ToString();

    [Fact]
    public async Task Checkout_books_the_held_seats_at_the_venue_price_and_ends_the_session()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var user = Guid.NewGuid();
        await fx.HoldSeatsAsync(eventId, user, seatIds[0], seatIds[1]);

        var result = await fx.NewBookingService().CheckoutAsync(
            eventId, user, [seatIds[0], seatIds[1]], PaymentMethod.PayAtCounter, NewKey());

        Assert.Equal(CheckoutOutcome.Booked, result.Outcome);
        var booking = result.Booking!;
        Assert.Equal(2, booking.Seats.Count);
        Assert.Equal(400m, booking.TotalAmount); // 2 × EGP 200
        Assert.StartsWith("SR-", booking.Reference);

        await using var db = fx.NewDbContext();
        Assert.Equal(2, await db.Seats.CountAsync(s => (s.Id == seatIds[0] || s.Id == seatIds[1]) && s.Status == SeatStatus.Booked));

        Assert.Empty(await fx.NewHoldService().GetActiveHoldsAsync(eventId));          // holds released
        Assert.Null(await fx.NewSessionService().GetActiveAsync(eventId, user));        // session ended

        var map = (await fx.NewSeatService().GetSeatMapAsync(eventId, user)).Seats;
        Assert.Equal(SeatStatus.Booked, map.Single(s => s.Id == seatIds[0]).Status);

        var pushes = fx.Notifier.For(eventId).Where(c => c.Status == SeatStatus.Booked).Select(c => c.SeatId);
        Assert.Equal([seatIds[0], seatIds[1]], pushes.Order());
    }

    [Fact]
    public async Task Same_idempotency_key_returns_the_original_booking()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var user = Guid.NewGuid();
        await fx.HoldSeatsAsync(eventId, user, seatIds[0]);
        var key = NewKey();

        var first = await fx.NewBookingService().CheckoutAsync(eventId, user, [seatIds[0]], PaymentMethod.PayAtCounter, key);
        var retry = await fx.NewBookingService().CheckoutAsync(eventId, user, [seatIds[0]], PaymentMethod.PayAtCounter, key);

        Assert.Equal(CheckoutOutcome.Booked, first.Outcome);
        Assert.Equal(CheckoutOutcome.Replayed, retry.Outcome);
        Assert.Equal(first.Booking!.Id, retry.Booking!.Id);
        Assert.Single(await fx.NewBookingService().GetBookingsAsync(user));
    }

    [Fact]
    public async Task Concurrent_retries_with_the_same_key_create_exactly_one_booking()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var user = Guid.NewGuid();
        await fx.HoldSeatsAsync(eventId, user, seatIds[0], seatIds[1]);
        var key = NewKey();

        var start = new TaskCompletionSource();
        var attempts = Enumerable.Range(0, 10).Select(async _ =>
        {
            await start.Task;
            return await fx.NewBookingService().CheckoutAsync(eventId, user, [seatIds[0], seatIds[1]], PaymentMethod.PayAtCounter, key);
        }).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, r => r.Outcome == CheckoutOutcome.Booked);
        Assert.All(results, r => Assert.Equal(results.First(x => x.Booking is not null).Booking!.Id, r.Booking!.Id));
        Assert.Single(await fx.NewBookingService().GetBookingsAsync(user));
    }

    [Fact]
    public async Task Cannot_book_seats_you_do_not_hold()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var (holder, other) = (Guid.NewGuid(), Guid.NewGuid());
        await fx.HoldSeatsAsync(eventId, holder, seatIds[0]);
        await fx.NewSessionService().GetOrStartAsync(eventId, other);

        var result = await fx.NewBookingService().CheckoutAsync(eventId, other, [seatIds[0]], PaymentMethod.PayAtCounter, NewKey());

        Assert.Equal(CheckoutOutcome.SeatsNotHeld, result.Outcome);
    }

    [Fact]
    public async Task Cannot_book_after_the_session_expired()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var user = Guid.NewGuid();
        var shortSessions = fx.NewSessionService(TimeSpan.FromSeconds(1));
        await shortSessions.GetOrStartAsync(eventId, user);
        await fx.NewHoldService(shortSessions).HoldAsync(seatIds[0], user);

        await Task.Delay(TimeSpan.FromSeconds(1.5));

        var result = await fx.NewBookingService().CheckoutAsync(eventId, user, [seatIds[0]], PaymentMethod.PayAtCounter, NewKey());
        Assert.Equal(CheckoutOutcome.SeatsNotHeld, result.Outcome);
    }

    [Fact]
    public async Task Postgres_never_books_a_seat_twice_even_if_redis_is_bypassed()
    {
        // Simulates a lost Redis lock: two users both "hold" the same seat (we write the key by hand),
        // then both check out at the same moment. Postgres must let exactly one through.
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var redis = fx.Redis.GetDatabase();
        var holdKey = $"hold:{{e:{eventId}}}:{seatIds[0]}";

        await redis.StringSetAsync(holdKey, a.ToString("N"));
        var bookingA = fx.NewBookingService().CheckoutAsync(eventId, a, [seatIds[0]], PaymentMethod.PayAtCounter, NewKey());
        await redis.StringSetAsync(holdKey, b.ToString("N"));
        var bookingB = fx.NewBookingService().CheckoutAsync(eventId, b, [seatIds[0]], PaymentMethod.PayAtCounter, NewKey());

        var results = await Task.WhenAll(bookingA, bookingB);

        Assert.True(results.Count(r => r.Outcome == CheckoutOutcome.Booked) <= 1);
        await using var db = fx.NewDbContext();
        Assert.True(await db.BookingItems.CountAsync(i => i.SeatId == seatIds[0]) <= 1);
    }

    [Fact]
    public async Task My_bookings_are_newest_first_and_only_mine()
    {
        var (eventId, seatIds) = await fx.CreateShowingAsync();
        var (me, someoneElse) = (Guid.NewGuid(), Guid.NewGuid());

        await fx.HoldSeatsAsync(eventId, me, seatIds[0]);
        await fx.NewBookingService().CheckoutAsync(eventId, me, [seatIds[0]], PaymentMethod.PayAtCounter, NewKey());
        await fx.HoldSeatsAsync(eventId, me, seatIds[1]);
        await fx.NewBookingService().CheckoutAsync(eventId, me, [seatIds[1]], PaymentMethod.PayAtCounter, NewKey());
        await fx.HoldSeatsAsync(eventId, someoneElse, seatIds[2]);
        await fx.NewBookingService().CheckoutAsync(eventId, someoneElse, [seatIds[2]], PaymentMethod.PayAtCounter, NewKey());

        var mine = await fx.NewBookingService().GetBookingsAsync(me);

        Assert.Equal(2, mine.Count);
        Assert.Equal(seatIds[1], mine[0].Seats.Single().SeatId);
        Assert.Equal(seatIds[0], mine[1].Seats.Single().SeatId);
    }
}
