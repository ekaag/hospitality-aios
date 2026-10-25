using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.HumanActions.Events;

/// <summary>
/// The assignee started the work (spec §8); the parent service request moves to in progress.
/// </summary>
public sealed record HumanActionStarted(
    HumanActionId HumanActionId,
    StaffMemberId StaffMemberId,
    ServiceRequestId? ServiceRequestId,
    DateTimeOffset OccurredAt) : IDomainEvent;
