namespace Infrastructure.Services;

public class WaitingRoomOptions
{
    public const string SectionName = "WaitingRoom";

    public int Capacity { get; set; } = 50;

    public TimeSpan PassDuration { get; set; } = TimeSpan.FromSeconds(60);

    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromSeconds(20);

    public TimeSpan AdmitInterval { get; set; } = TimeSpan.FromSeconds(1);
}
