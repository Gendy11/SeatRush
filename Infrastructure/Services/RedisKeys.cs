namespace Infrastructure.Services;

public static class RedisKeys
{
    private static string Tag(int eventId) => $"{{e:{eventId}}}";

    public static string Session(int eventId, Guid userId) => $"session:{Tag(eventId)}:{userId:N}";

    public static string Hold(int eventId, int seatId) => $"hold:{Tag(eventId)}:{seatId}";

    public static string HoldIndex(int eventId) => $"holds:{Tag(eventId)}";

    public static string RefreshToken(string tokenHash) => $"rt:{tokenHash}";

    public static string RefreshFamily(string familyId) => $"rtfam:{familyId}";

    public static string Seq(int eventId) => $"seq:{Tag(eventId)}";

    public static string Queue(int eventId) => $"queue:{Tag(eventId)}";

    public static string QueueTicket(int eventId) => $"queue-ticket:{Tag(eventId)}";

    public static string QueueSeen(int eventId) => $"queue-seen:{Tag(eventId)}";

    public static string QueueSkipped(int eventId) => $"queue-skipped:{Tag(eventId)}";

    public static string Room(int eventId) => $"room:{Tag(eventId)}";

    public static string RoomPass(int eventId) => $"room-pass:{Tag(eventId)}";

    public static string RoomLock(int eventId) => $"room-lock:{Tag(eventId)}";

    public const string ActiveQueues = "queues";
}
