using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.HumanActions.Events;

/// <summary>
/// The work is no longer needed.
/// </summary>
public sealed record HumanActionCancelled(
    HumanActionId HumanActionId,
    ServiceRequestId? ServiceRequestId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
