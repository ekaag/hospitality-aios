using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Operations;

/// <summary>
/// Version 1 of <c>HumanActionAssigned</c>: physical work was assigned or reassigned to a staff member.
/// </summary>
/// <param name="HumanActionId">The human action.</param>
/// <param name="StaffMemberId">The staff member.</param>
/// <param name="AssignedBy">Who assigned it (router or manager).</param>
public sealed record HumanActionAssignedV1(
    Guid HumanActionId,
    StaffMemberId StaffMemberId,
    string AssignedBy) : IEventPayload;
