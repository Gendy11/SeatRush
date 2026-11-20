namespace Infrastructure.Services;

/// <summary>
/// All Redis keys in one place. The {e:id} hash tag puts every key of a showing on the same
/// Redis Cluster slot, so multi-key transactions on one showing keep working when Redis is sharded.
/// </summary>
public static class RedisKeys
{
    private static string Tag(int eventId) => $"{{e:{eventId}}}";

    /// <summary>String: session expiry (unix ms). TTL = session length.</summary>
    public static string Session(int eventId, Guid userId) => $"session:{Tag(eventId)}:{userId:N}";

    /// <summary>String: id of the user holding the seat. TTL = until the holder's session ends.</summary>
    public static string Hold(int eventId, int seatId) => $"hold:{Tag(eventId)}:{seatId}";

    /// <summary>
    /// Sorted set of held seat ids, scored by hold expiry (unix ms). Lets the seat map find all
    /// current holds in one call (ZRANGEBYSCORE now +inf) instead of scanning keys.
    /// </summary>
    public static string HoldIndex(int eventId) => $"holds:{Tag(eventId)}";

    /// <summary>Integer: per-showing change counter, INCR'd in the same transaction as every seat change.</summary>
    public static string Seq(int eventId) => $"seq:{Tag(eventId)}";
}
