namespace Core.Models;

public enum RushMode
{
    Normal,

    Extreme
}

public enum RushStartOutcome
{
    Started,
    NotFound,
    ShowingStarted,
    AlreadyRunning,

    CoolingDown
}

public record RushStatus(bool Running, RushMode? Mode, int Bots, int ActiveBots, int SeatsBooked, DateTime? StartedAt);

public record RushStartResult(RushStartOutcome Outcome, RushMode? Mode = null, long? QueuePosition = null);
