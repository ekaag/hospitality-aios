using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Services;

/// <summary>
/// Version 1 of <c>ServiceRequestUpdated</c>: a service request moved to another unresolved state (in progress, exception, cancelled).
/// </summary>
/// <param name="ServiceRequestId">The service request.</param>
/// <param name="Status">The status after the change (domain enum name).</param>
/// <param name="ExceptionReason">Why the request is in exception, if it is.</param>
public sealed record ServiceRequestUpdatedV1(
    ServiceRequestId ServiceRequestId,
    string Status,
    string? ExceptionReason) : IEventPayload;
