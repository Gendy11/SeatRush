namespace Core.Models;

public enum QueueState
{
    NotQueued,

    Waiting,

    Admitted
}

public record QueueStatus(QueueState State, long? Position = null, DateTime? AdmittedUntil = null);
