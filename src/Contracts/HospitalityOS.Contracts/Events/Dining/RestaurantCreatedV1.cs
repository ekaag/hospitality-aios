using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Dining;

/// <summary>
/// Version 1 of <c>RestaurantCreated</c>: a restaurant was created (Journey 3).
/// </summary>
/// <param name="RestaurantId">The restaurant.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record RestaurantCreatedV1(
    Guid RestaurantId,
    PropertyId PropertyId,
    RestaurantDetailsData Details) : IEventPayload;
