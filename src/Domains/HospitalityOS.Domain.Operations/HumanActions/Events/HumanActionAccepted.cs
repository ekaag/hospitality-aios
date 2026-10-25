using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.HumanActions.Events;

/// <summary>
/// The assignee accepted the work (spec §8).
/// </summary>
public sealed record HumanActionAccepted(
    HumanActionId HumanActionId,
    StaffMemberId StaffMemberId,
    DateTimeOffset OccurredAt) : IDomainEvent;
