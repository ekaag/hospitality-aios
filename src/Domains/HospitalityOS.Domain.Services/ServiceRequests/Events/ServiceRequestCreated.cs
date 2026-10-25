using HospitalityOS.Domain.Services.Services;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Services.ServiceRequests.Events;

/// <summary>
/// A service request was created (spec §8); the Guest Service controller reacts by requesting a human action.
/// </summary>
public sealed record ServiceRequestCreated(
    ServiceRequestId ServiceRequestId,
    PropertyId PropertyId,
    ServiceId ServiceId,
    ServiceCategory ServiceCategory,
    ServiceRequestDetails Details,
    RequestOrigin Origin,
    DateTimeOffset? DueAt,
    DateTimeOffset OccurredAt) : IDomainEvent;
