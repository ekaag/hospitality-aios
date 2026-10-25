using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.HumanActions.Events;

/// <summary>
/// The assignee could not do the work (spec §8, Journey 20); the parent request stays unresolved.
/// </summary>
public sealed record HumanActionFailed(
    HumanActionId HumanActionId,
    StaffMemberId StaffMemberId,
    ServiceRequestId? ServiceRequestId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
