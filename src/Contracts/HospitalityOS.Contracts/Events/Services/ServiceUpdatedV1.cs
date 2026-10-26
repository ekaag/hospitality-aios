using HospitalityOS.Contracts.Events.Values;

namespace HospitalityOS.Contracts.Events.Services;

/// <summary>
/// Version 1 of <c>ServiceUpdated</c>: a service changed.
/// </summary>
/// <param name="ServiceId">The service.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record ServiceUpdatedV1(
    Guid ServiceId,
    ServiceDetailsData Details) : IEventPayload;
