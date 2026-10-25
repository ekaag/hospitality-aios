using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Services.ServiceRequests.Events;

/// <summary>
/// A service request was fulfilled (spec §8); raised exactly once, and triggers the guest notification.
/// </summary>
public sealed record ServiceRequestCompleted(
    ServiceRequestId ServiceRequestId,
    PropertyId PropertyId,
    RequestOrigin Origin,
    DateTimeOffset OccurredAt) : IDomainEvent;
