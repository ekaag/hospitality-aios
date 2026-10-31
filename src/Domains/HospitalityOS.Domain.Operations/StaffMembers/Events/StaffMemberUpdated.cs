using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.StaffMembers.Events;

/// <summary>
/// A staff member's name or skills changed.
/// </summary>
public sealed record StaffMemberUpdated(
    StaffMemberId StaffMemberId,
    StaffMemberDetails Details,
    DateTimeOffset OccurredAt) : IDomainEvent;
