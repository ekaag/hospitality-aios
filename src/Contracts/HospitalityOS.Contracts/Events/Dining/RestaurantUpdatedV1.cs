using HospitalityOS.Contracts.Events.Values;

namespace HospitalityOS.Contracts.Events.Dining;

/// <summary>
/// Version 1 of <c>RestaurantUpdated</c>: a restaurant changed.
/// </summary>
/// <param name="RestaurantId">The restaurant.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record RestaurantUpdatedV1(
    Guid RestaurantId,
    RestaurantDetailsData Details) : IEventPayload;
