using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Operations;

/// <summary>
/// Version 1 of <c>HumanActionExpired</c>: the deadline passed before the work started.
/// </summary>
/// <param name="HumanActionId">The human action.</param>
/// <param name="ServiceRequestId">The service request.</param>
public sealed record HumanActionExpiredV1(
    Guid HumanActionId,
    ServiceRequestId? ServiceRequestId) : IEventPayload;
