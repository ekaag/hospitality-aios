using HospitalityOS.Ports.Eventing;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// A queued event's publish state. Mutated only under the store's lock.
/// </summary>
internal sealed class OutboxRow(long position, EventMessage message)
{
    public long Position { get; } = position;

    public EventMessage Message { get; } = message;

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public DateTimeOffset? LeaseUntil { get; set; }

    public DateTimeOffset? DeadLetteredAt { get; set; }

    public bool IsDueAt(DateTimeOffset now) =>
        DeadLetteredAt is null && (NextAttemptAt is null || NextAttemptAt <= now) && (LeaseUntil is null || LeaseUntil <= now);
}
