using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Operations;

/// <summary>
/// Version 1 of <c>HumanActionFailed</c>: the assignee could not do the work (Journey 20).
/// </summary>
/// <param name="HumanActionId">The human action.</param>
/// <param name="StaffMemberId">The staff member.</param>
/// <param name="ServiceRequestId">The service request.</param>
/// <param name="Reason">Why, in the worker's or system's words.</param>
public sealed record HumanActionFailedV1(
    Guid HumanActionId,
    StaffMemberId StaffMemberId,
    ServiceRequestId? ServiceRequestId,
    string Reason) : IEventPayload;
