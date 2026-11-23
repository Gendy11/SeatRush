namespace Core.Models;

public static class RushBots
{
    public const string IdPrefix = "b0700000-0000-4000-8000-";

    public static Guid Id(long number) => Guid.Parse(IdPrefix + number.ToString("x12"));

    public static bool IsBot(Guid userId) => userId.ToString().StartsWith(IdPrefix, StringComparison.Ordinal);
}
