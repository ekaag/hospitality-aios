using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Operations;

/// <summary>
/// Version 1 of <c>HumanActionStarted</c>: the assignee started the work.
/// </summary>
/// <param name="HumanActionId">The human action.</param>
/// <param name="StaffMemberId">The staff member.</param>
/// <param name="ServiceRequestId">The service request.</param>
public sealed record HumanActionStartedV1(
    Guid HumanActionId,
    StaffMemberId StaffMemberId,
    ServiceRequestId? ServiceRequestId) : IEventPayload;
