using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Domain.Operations.StaffMembers;

/// <summary>
/// Who a staff member is to the router. Validated and normalized by <see cref="StaffMember"/>.
/// </summary>
/// <param name="DisplayName">The name staff and managers see, e.g. <c>Sam Patel</c>; required.</param>
/// <param name="Skills">Skill codes the router matches against a human action's required skills, e.g.
/// <c>housekeeping</c>; normalized to lowercase, unique, and sorted.</param>
public sealed record StaffMemberDetails(string DisplayName, ValueList<string>? Skills = null);
