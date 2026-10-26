using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Persistence;

/// <summary>
/// An aggregate a scope added or loaded, and the committed version it is based on.
/// </summary>
internal sealed class TrackedAggregate(Guid id, TenantId tenantId, IAggregateRoot aggregate, long? committedVersion)
{
    public Guid Id { get; } = id;

    public TenantId TenantId { get; } = tenantId;

    public IAggregateRoot Aggregate { get; } = aggregate;

    /// <summary>
    /// The version last loaded or committed; <c>null</c> until a new aggregate is first committed.
    /// </summary>
    public long? CommittedVersion { get; set; } = committedVersion;

    public bool HasChanges => CommittedVersion != Aggregate.Version;
}
