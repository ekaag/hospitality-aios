using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.HumanActions.Events;

/// <summary>
/// The work was assigned or reassigned to a staff member (spec §8).
/// </summary>
public sealed record HumanActionAssigned(
    HumanActionId HumanActionId,
    StaffMemberId StaffMemberId,
    string AssignedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;
