using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Integrations.ExternalReferences.Events;

/// <summary>
/// A provider's ID was linked to a canonical record.
/// </summary>
public sealed record ExternalReferenceCreated(
    ExternalReferenceId ExternalReferenceId,
    PropertyId PropertyId,
    string ProviderName,
    string EntityType,
    string ExternalId,
    Guid CanonicalId,
    DateTimeOffset OccurredAt) : IDomainEvent;
