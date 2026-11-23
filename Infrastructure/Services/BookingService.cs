using System.Security.Cryptography;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Services;

public class BookingService(
    SeatRushContext db,
    ISeatHoldService holds,
    IBookingSessionService sessions,
    TimeProvider clock,
    ILogger<BookingService> logger) : IBookingService
{
    public async Task<CheckoutResult> CheckoutAsync(
        int eventId,
        Guid userId,
        IReadOnlyCollection<int> seatIds,
        PaymentMethod paymentMethod,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (await FindByIdempotencyKeyAsync(userId, idempotencyKey, ct) is { } replay)
            return new CheckoutResult(CheckoutOutcome.Replayed, replay);

        var ids = seatIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new CheckoutResult(CheckoutOutcome.NoSeats);

        var price = await db.Events
            .Where(e => e.Id == eventId)
            .Select(e => (decimal?)e.Venue!.TicketPrice)
            .FirstOrDefaultAsync(ct);

        var seatsInEvent = await db.Seats.CountAsync(s => s.EventId == eventId && ids.Contains(s.Id), ct);

        if (price is null || seatsInEvent != ids.Length || !await holds.AreAllHeldByAsync(eventId, ids, userId))
            return new CheckoutResult(CheckoutOutcome.SeatsNotHeld);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // only flips seats that are still available, so the db itself blocks double booking
            var updated = await db.Seats
                .Where(s => s.EventId == eventId && ids.Contains(s.Id) && s.Status == SeatStatus.Available)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.Status, SeatStatus.Booked)
                    .SetProperty(s => s.Version, s => s.Version + 1), ct);

            if (updated != ids.Length)
            {
                await tx.RollbackAsync(ct);
                return await ReplayOr(CheckoutOutcome.SeatsUnavailable, userId, idempotencyKey, ct);
            }

            var booking = new Booking
            {
                Reference = NewReference(),
                EventId = eventId,
                UserId = userId,
                IdempotencyKey = idempotencyKey,
                Status = BookingStatus.Confirmed,
                PaymentMethod = paymentMethod,
                TotalAmount = price.Value * ids.Length,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
                Items = ids.Select(id => new BookingItem { SeatId = id, Price = price.Value }).ToList()
            };

            db.Bookings.Add(booking);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return await ReplayOr(CheckoutOutcome.SeatsUnavailable, userId, idempotencyKey, ct);
        }

        try
        {
            await holds.MarkBookedAsync(eventId, ids);
            await sessions.EndAsync(eventId, userId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Booking for event {EventId} committed but Redis cleanup failed", eventId);
        }

        var summary = await FindByIdempotencyKeyAsync(userId, idempotencyKey, ct);
        return new CheckoutResult(CheckoutOutcome.Booked, summary);
    }

    public async Task<IReadOnlyList<BookingSummary>> GetBookingsAsync(Guid userId, CancellationToken ct = default) =>
        await Summaries(db.Bookings.Where(b => b.UserId == userId).OrderByDescending(b => b.CreatedAt))
            .ToListAsync(ct);

    private async Task<CheckoutResult> ReplayOr(CheckoutOutcome outcome, Guid userId, string key, CancellationToken ct) =>
        await FindByIdempotencyKeyAsync(userId, key, ct) is { } replay
            ? new CheckoutResult(CheckoutOutcome.Replayed, replay)
            : new CheckoutResult(outcome);

    private Task<BookingSummary?> FindByIdempotencyKeyAsync(Guid userId, string key, CancellationToken ct) =>
        Summaries(db.Bookings.Where(b => b.UserId == userId && b.IdempotencyKey == key)).FirstOrDefaultAsync(ct);

    private static IQueryable<BookingSummary> Summaries(IQueryable<Booking> bookings) =>
        bookings.AsNoTracking().Select(b => new BookingSummary(
            b.Id,
            b.Reference,
            b.EventId,
            b.Event!.Movie!.Title,
            b.Event.Movie.PosterUrl,
            b.Event.Venue!.Name,
            b.Event.ShowTime,
            b.Items
                .OrderBy(i => i.SeatId)
                .Select(i => new BookedSeat(i.SeatId, i.Seat!.VenueSeat!.Row, i.Seat.VenueSeat.Number, i.Price))
                .ToList(),
            b.TotalAmount,
            b.PaymentMethod,
            b.Status,
            b.CreatedAt));

    private const string ReferenceAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    private static string NewReference() =>
        "SR-" + RandomNumberGenerator.GetString(ReferenceAlphabet, 8);
}
