using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Organizations.Access.Events;

/// <summary>
/// Someone was given access to the tenant and invited with a code.
/// </summary>
public sealed record StaffAccessGranted(
    StaffAccessId StaffAccessId,
    StaffRole Role,
    StaffMemberId? StaffMemberId,
    DateTimeOffset OccurredAt) : IDomainEvent;
