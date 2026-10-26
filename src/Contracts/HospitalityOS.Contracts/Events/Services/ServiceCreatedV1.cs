using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Services;

/// <summary>
/// Version 1 of <c>ServiceCreated</c>: a requestable service was created.
/// </summary>
/// <param name="ServiceId">The service.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record ServiceCreatedV1(
    Guid ServiceId,
    PropertyId PropertyId,
    ServiceDetailsData Details) : IEventPayload;
