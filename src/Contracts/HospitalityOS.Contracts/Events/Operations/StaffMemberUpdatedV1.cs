using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Operations;

/// <summary>
/// Version 1 of <c>StaffMemberUpdated</c>: a staff member's name or skills changed.
/// </summary>
/// <param name="StaffMemberId">The staff member.</param>
/// <param name="DisplayName">Their name after the change.</param>
/// <param name="Skills">Their skill codes after the change.</param>
public sealed record StaffMemberUpdatedV1(
    StaffMemberId StaffMemberId,
    string DisplayName,
    ValueList<string> Skills) : IEventPayload;
