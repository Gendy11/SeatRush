namespace Core.Models;

public enum HoldOutcome
{
    Held,
    SeatNotFound,
    SeatUnavailable,
    HoldLimitReached
}

public record HoldResult(HoldOutcome Outcome, DateTime? ExpiresAt = null)
{
    public bool Succeeded => Outcome == HoldOutcome.Held;
}
