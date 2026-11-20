namespace Infrastructure.Services;

public class SeatHoldOptions
{
    public const string SectionName = "SeatHolds";

    public TimeSpan HoldDuration { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxHeldSeatsPerUser { get; set; } = 4;
}
