namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// One aggregate to save in a commit.
/// </summary>
/// <param name="Aggregate">The committed state, already copied and cleared of events.</param>
/// <param name="ExpectedVersion">The version it was loaded at, or <c>null</c> for a new aggregate.</param>
internal sealed record PendingWrite(StoredAggregate Aggregate, long? ExpectedVersion)
{
    public bool IsNew => ExpectedVersion is null;
}
