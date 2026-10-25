using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Services.ServiceRequests.Events;

/// <summary>
/// A service request moved to in-progress, exception, or cancelled (spec §8).
/// </summary>
public sealed record ServiceRequestUpdated(
    ServiceRequestId ServiceRequestId,
    ServiceRequestStatus Status,
    string? ExceptionReason,
    DateTimeOffset OccurredAt) : IDomainEvent;
