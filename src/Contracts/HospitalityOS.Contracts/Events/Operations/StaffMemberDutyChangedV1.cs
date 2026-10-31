using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Operations;

/// <summary>
/// Version 1 of <c>StaffMemberDutyChanged</c>: a staff member came on or went off duty.
/// </summary>
/// <param name="StaffMemberId">The staff member.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="IsOnDuty">Whether they are on duty now.</param>
public sealed record StaffMemberDutyChangedV1(
    StaffMemberId StaffMemberId,
    PropertyId PropertyId,
    bool IsOnDuty) : IEventPayload;
