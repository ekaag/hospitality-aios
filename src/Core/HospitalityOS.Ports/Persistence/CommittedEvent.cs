using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Ports.Persistence;

/// <summary>
/// A domain event saved in the same commit as the aggregate that raised it. The outbox (C5) stores these records
/// in that same transaction and wraps each in the canonical envelope (C3).
/// </summary>
/// <param name="TenantId">The tenant that owns the aggregate.</param>
/// <param name="PropertyId">The property the aggregate belongs to, or <c>null</c> for tenant-wide aggregates (organizations, guests).</param>
/// <param name="AggregateType">The aggregate's type name, e.g. <c>HumanAction</c>.</param>
/// <param name="AggregateId">The aggregate's ID.</param>
/// <param name="AggregateVersion">The aggregate's version immediately after this event (1 for its creation event).</param>
/// <param name="Event">The event.</param>
public sealed record CommittedEvent(TenantId TenantId, PropertyId? PropertyId, string AggregateType, Guid AggregateId, long AggregateVersion, IDomainEvent Event);
