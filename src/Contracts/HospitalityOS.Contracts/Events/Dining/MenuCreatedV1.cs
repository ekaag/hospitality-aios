using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Dining;

/// <summary>
/// Version 1 of <c>MenuCreated</c>: a menu was created.
/// </summary>
/// <param name="MenuId">The menu.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="RestaurantId">The restaurant.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record MenuCreatedV1(
    Guid MenuId,
    PropertyId PropertyId,
    Guid RestaurantId,
    MenuDetailsData Details) : IEventPayload;
