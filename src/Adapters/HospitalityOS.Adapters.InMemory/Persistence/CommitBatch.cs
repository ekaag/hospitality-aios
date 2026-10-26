using HospitalityOS.Ports.Eventing;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// Everything one unit of work saves at once: changed aggregates, appended events, and inbox records.
/// </summary>
internal sealed record CommitBatch(IReadOnlyList<PendingWrite> Aggregates, IReadOnlyList<EventMessage> Events, IReadOnlyList<InboxKey> InboxRecords)
{
    public bool IsEmpty => Aggregates.Count == 0 && Events.Count == 0 && InboxRecords.Count == 0;
}
