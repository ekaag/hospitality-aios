using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Services;

/// <summary>
/// Version 1 of <c>ServiceRequestCompleted</c>: a service request was fulfilled.
/// </summary>
/// <param name="ServiceRequestId">The service request.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Origin">Who or what asked for it.</param>
public sealed record ServiceRequestCompletedV1(
    ServiceRequestId ServiceRequestId,
    PropertyId PropertyId,
    RequestOriginData Origin) : IEventPayload;
