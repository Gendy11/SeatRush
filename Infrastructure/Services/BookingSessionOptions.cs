namespace Infrastructure.Services;

public class BookingSessionOptions
{
    public const string SectionName = "BookingSession";

    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(7);
}
