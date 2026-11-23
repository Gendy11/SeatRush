namespace Infrastructure.Demo;

public class RushOptions
{
    public const string SectionName = "Rush";

    public TimeSpan NormalArrivalMin { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan NormalArrivalMax { get; set; } = TimeSpan.FromSeconds(15);

    public int ExtremeBots { get; set; } = 300;
    public TimeSpan ExtremeArrivalWindow { get; set; } = TimeSpan.FromSeconds(5);

    public int StarterJoinsAfter { get; set; } = 150;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);

    public TimeSpan LookMin { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan LookMax { get; set; } = TimeSpan.FromSeconds(3);

    public TimeSpan ClickGapMin { get; set; } = TimeSpan.FromMilliseconds(300);
    public TimeSpan ClickGapMax { get; set; } = TimeSpan.FromMilliseconds(900);

    public TimeSpan DecideMin { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan DecideMax { get; set; } = TimeSpan.FromSeconds(20);

    public double NormalBookChance { get; set; } = 0.7;
    public double ExtremeBookChance { get; set; } = 0.8;

    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(30);
}
