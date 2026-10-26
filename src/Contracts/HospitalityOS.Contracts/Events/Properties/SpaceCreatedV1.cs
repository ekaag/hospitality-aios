using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>SpaceCreated</c>: a space (building, floor, room, area) was created.
/// </summary>
/// <param name="SpaceId">The space.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="ParentSpaceId">The containing space, or <c>null</c> for a top-level space.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record SpaceCreatedV1(
    SpaceId SpaceId,
    PropertyId PropertyId,
    SpaceId? ParentSpaceId,
    SpaceDetailsData Details) : IEventPayload;
