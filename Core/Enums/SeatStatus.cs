namespace Core.Enums;

public enum SeatStatus
{
    Available,

    /// <summary>Never stored in Postgres: derived from an active hold in Redis.</summary>
    Held,

    Booked
}
