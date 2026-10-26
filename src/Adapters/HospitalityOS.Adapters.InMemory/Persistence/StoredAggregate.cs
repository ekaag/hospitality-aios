using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// A committed aggregate. <see cref="Snapshot"/> is owned by the store, holds no pending events, and is never
/// handed out; readers get a copy.
/// </summary>
internal sealed record StoredAggregate(AggregateKey Key, TenantId TenantId, IAggregateRoot Snapshot)
{
    public long Version => Snapshot.Version;
}
