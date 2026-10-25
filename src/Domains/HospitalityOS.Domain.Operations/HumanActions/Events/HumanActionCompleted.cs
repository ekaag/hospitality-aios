using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.HumanActions.Events;

/// <summary>
/// The work was done (spec §8); raised exactly once. The Guest Service controller resumes on it.
/// </summary>
public sealed record HumanActionCompleted(
    HumanActionId HumanActionId,
    StaffMemberId StaffMemberId,
    ServiceRequestId? ServiceRequestId,
    DateTimeOffset OccurredAt) : IDomainEvent;
