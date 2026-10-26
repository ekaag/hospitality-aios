using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// An aggregate a scope added or loaded, and the committed version it is based on.
/// </summary>
internal sealed class TrackedAggregate(AggregateKey key, TenantId tenantId, IAggregateRoot aggregate, long? committedVersion)
{
    public AggregateKey Key { get; } = key;

    public TenantId TenantId { get; } = tenantId;

    public IAggregateRoot Aggregate { get; } = aggregate;

    /// <summary>
    /// The version last loaded or committed; <c>null</c> until a new aggregate is first committed.
    /// </summary>
    public long? CommittedVersion { get; set; } = committedVersion;

    public bool HasChanges => CommittedVersion != Aggregate.Version;
}
