namespace Core.Models;

public enum HoldOutcome
{
    Held,
    SeatNotFound,

    /// <summary>Booked, or held by someone else.</summary>
    SeatUnavailable,

    /// <summary>The user has no active booking session for this showing (never started, or the 7 minutes ran out).</summary>
    NoActiveSession
}

public record HoldResult(HoldOutcome Outcome, DateTime? ExpiresAt = null)
{
    public bool Succeeded => Outcome == HoldOutcome.Held;
}
