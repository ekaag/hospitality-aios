using HospitalityOS.Contracts.Events.Values;

namespace HospitalityOS.Contracts.Events.Dining;

/// <summary>
/// Version 1 of <c>MenuUpdated</c>: a menu or the availability of one of its items changed.
/// </summary>
/// <param name="MenuId">The menu.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record MenuUpdatedV1(
    Guid MenuId,
    MenuDetailsData Details) : IEventPayload;
