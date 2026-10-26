using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Operations;

/// <summary>
/// Version 1 of <c>HumanActionCancelled</c>: the work was cancelled before it started.
/// </summary>
/// <param name="HumanActionId">The human action.</param>
/// <param name="ServiceRequestId">The service request.</param>
/// <param name="Reason">Why, in the worker's or system's words.</param>
public sealed record HumanActionCancelledV1(
    Guid HumanActionId,
    ServiceRequestId? ServiceRequestId,
    string Reason) : IEventPayload;
