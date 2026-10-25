using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.HumanActions.Events;

/// <summary>
/// The deadline passed before the work started.
/// </summary>
public sealed record HumanActionExpired(
    HumanActionId HumanActionId,
    ServiceRequestId? ServiceRequestId,
    DateTimeOffset OccurredAt) : IDomainEvent;
