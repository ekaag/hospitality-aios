using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Operations;

/// <summary>
/// Version 1 of <c>HumanActionAccepted</c>: the assignee accepted the work.
/// </summary>
/// <param name="HumanActionId">The human action.</param>
/// <param name="StaffMemberId">The staff member.</param>
public sealed record HumanActionAcceptedV1(
    Guid HumanActionId,
    StaffMemberId StaffMemberId) : IEventPayload;
