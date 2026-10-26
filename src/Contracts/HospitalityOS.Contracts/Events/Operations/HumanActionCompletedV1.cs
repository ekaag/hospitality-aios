using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Operations;

/// <summary>
/// Version 1 of <c>HumanActionCompleted</c>: the assignee completed the work (Journey 14); resumes the parent workflow.
/// </summary>
/// <param name="HumanActionId">The human action.</param>
/// <param name="StaffMemberId">The staff member.</param>
/// <param name="ServiceRequestId">The service request.</param>
public sealed record HumanActionCompletedV1(
    Guid HumanActionId,
    StaffMemberId StaffMemberId,
    ServiceRequestId? ServiceRequestId) : IEventPayload;
