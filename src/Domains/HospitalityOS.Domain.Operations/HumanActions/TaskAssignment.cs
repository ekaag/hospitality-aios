using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.HumanActions;

/// <summary>
/// One assignment of a <see cref="HumanAction"/> to a staff member. Reassignment appends a new one, so the
/// history shows who held the work and who decided (router, agent, or manager).
/// </summary>
/// <param name="StaffMemberId">Who was assigned.</param>
/// <param name="AssignedBy">Who assigned them, e.g. <c>HumanActionRouter</c> or a manager's user ID.</param>
/// <param name="AssignedAt">When (UTC).</param>
public sealed record TaskAssignment(StaffMemberId StaffMemberId, string AssignedBy, DateTimeOffset AssignedAt);
