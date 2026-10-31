using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.StaffMembers.Events;

/// <summary>
/// A staff member joined a property's roster.
/// </summary>
public sealed record StaffMemberCreated(
    StaffMemberId StaffMemberId,
    PropertyId PropertyId,
    StaffMemberDetails Details,
    bool IsOnDuty,
    DateTimeOffset OccurredAt) : IDomainEvent;
