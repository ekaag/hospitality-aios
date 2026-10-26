using HospitalityOS.Contracts.Events;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Eventing.Contracts;

/// <summary>
/// A domain event's published payload, plus the organization when the event identifies it (the envelope's
/// optional <c>organizationId</c>).
/// </summary>
internal sealed record MappedEvent(IEventPayload Payload, OrganizationId? OrganizationId = null);
