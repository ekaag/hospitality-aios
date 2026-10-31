using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Operations;

/// <summary>
/// Version 1 of <c>StaffMemberCreated</c>: someone joined a property's roster.
/// </summary>
/// <param name="StaffMemberId">The staff member.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="DisplayName">Their name.</param>
/// <param name="Skills">Their skill codes.</param>
/// <param name="IsOnDuty">Whether they started on duty.</param>
public sealed record StaffMemberCreatedV1(
    StaffMemberId StaffMemberId,
    PropertyId PropertyId,
    string DisplayName,
    ValueList<string> Skills,
    bool IsOnDuty) : IEventPayload;
