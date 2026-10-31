using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Staff;

/// <summary>
/// Replaces a staff member's name and skills.
/// </summary>
/// <param name="DisplayName">Their name.</param>
/// <param name="Skills">Their skill codes; none when omitted.</param>
public sealed record UpdateStaffMemberRequest(string? DisplayName = null, ValueList<string>? Skills = null);
