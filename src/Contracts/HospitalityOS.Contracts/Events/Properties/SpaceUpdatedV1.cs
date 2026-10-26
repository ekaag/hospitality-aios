using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>SpaceUpdated</c>: a space's details changed.
/// </summary>
/// <param name="SpaceId">The space.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record SpaceUpdatedV1(
    SpaceId SpaceId,
    SpaceDetailsData Details) : IEventPayload;
