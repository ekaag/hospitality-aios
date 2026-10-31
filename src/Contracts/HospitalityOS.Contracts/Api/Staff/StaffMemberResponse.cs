using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Staff;

/// <summary>
/// A staff member on a property's roster.
/// </summary>
/// <param name="Id">Their staff member ID.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="DisplayName">Their name.</param>
/// <param name="Skills">Their skill codes, sorted.</param>
/// <param name="IsOnDuty">Whether they are on duty.</param>
/// <param name="Version">The version, for <c>If-Match</c>.</param>
public sealed record StaffMemberResponse(
    StaffMemberId Id,
    PropertyId PropertyId,
    string DisplayName,
    ValueList<string> Skills,
    bool IsOnDuty,
    long Version);
