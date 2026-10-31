using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Staff;

/// <summary>
/// Adds someone to a property's roster. Missing values are reported by the server, not the deserializer.
/// </summary>
/// <param name="Id">Their staff member ID, the subject their identity provider puts in their token; generated when
/// omitted.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="DisplayName">Their name, e.g. Sam Patel.</param>
/// <param name="Skills">Skill codes the router matches, e.g. housekeeping.</param>
/// <param name="IsOnDuty">Whether they start on duty; off duty when omitted.</param>
public sealed record CreateStaffMemberRequest(
    StaffMemberId? Id = null,
    PropertyId? PropertyId = null,
    string? DisplayName = null,
    ValueList<string>? Skills = null,
    bool? IsOnDuty = null);
