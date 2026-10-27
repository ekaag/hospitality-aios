using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Integrations;

/// <summary>
/// Version 1 of <c>ExternalReferenceCreated</c>: a provider's ID was linked to a canonical record.
/// </summary>
/// <param name="ExternalReferenceId">The reference.</param>
/// <param name="PropertyId">The property whose provider issued the ID.</param>
/// <param name="ProviderName">The provider's registered name.</param>
/// <param name="EntityType">The canonical record's type name, e.g. <c>StayContext</c>.</param>
/// <param name="ExternalId">The provider's ID.</param>
/// <param name="CanonicalId">The canonical record's ID.</param>
public sealed record ExternalReferenceCreatedV1(
    Guid ExternalReferenceId,
    PropertyId PropertyId,
    string ProviderName,
    string EntityType,
    string ExternalId,
    Guid CanonicalId) : IEventPayload;
