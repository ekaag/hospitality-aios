using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.StaffMembers.Events;

/// <summary>
/// A staff member came on or went off duty; the HumanAction Router reroutes work on it.
/// </summary>
public sealed record StaffMemberDutyChanged(
    StaffMemberId StaffMemberId,
    PropertyId PropertyId,
    bool IsOnDuty,
    DateTimeOffset OccurredAt) : IDomainEvent;
