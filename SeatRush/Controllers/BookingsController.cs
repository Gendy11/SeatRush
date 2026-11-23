using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatRush.RateLimiting;
using SeatRush.DTOs;

namespace SeatRush.Controllers;

[Authorize]
public class BookingsController(IBookingService bookings) : BaseApiController
{
    private const string IdempotencyHeader = "Idempotency-Key";

    /// <summary>
    /// FR15–FR17: book the seats the user holds. Requires an Idempotency-Key header; sending the same
    /// key again returns the original booking (200) instead of creating a second one.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitingSetup.Checkout)]
    public async Task<ActionResult<BookingDto>> Create(
        CreateBookingDto request,
        [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return MissingUser();

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"A valid {IdempotencyHeader} header is required.");

        if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, ignoreCase: true, out var paymentMethod))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unsupported payment method.");

        var result = await bookings.CheckoutAsync(request.EventId, userId, request.SeatIds, paymentMethod, idempotencyKey, ct);

        return result.Outcome switch
        {
            CheckoutOutcome.Booked => CreatedAtAction(nameof(GetMine), null, ToDto(result.Booking!)),
            CheckoutOutcome.Replayed => Ok(ToDto(result.Booking!)),
            CheckoutOutcome.NoSeats => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Select at least one seat."),
            CheckoutOutcome.SeatsNotHeld => Problem(statusCode: StatusCodes.Status409Conflict, title: "Some seats are no longer held by you. Your session may have expired."),
            CheckoutOutcome.SeatsUnavailable => Problem(statusCode: StatusCodes.Status409Conflict, title: "Some seats were already booked."),
            _ => throw new InvalidOperationException($"Unhandled checkout outcome {result.Outcome}")
        };
    }

    /// <summary>FR18: the current user's bookings, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetMine(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return MissingUser();

        var mine = await bookings.GetBookingsAsync(userId, ct);
        return Ok(mine.Select(ToDto).ToList());
    }

    private static BookingDto ToDto(BookingSummary b) => new(
        b.Id,
        b.Reference,
        b.EventId,
        b.MovieTitle,
        b.PosterUrl,
        b.Venue,
        b.ShowTime,
        b.Seats.Select(s => new BookedSeatDto(s.SeatId, s.Row, s.Number, s.Price)).ToList(),
        b.TotalAmount,
        b.PaymentMethod.ToString(),
        b.Status.ToString(),
        b.CreatedAt);
}
