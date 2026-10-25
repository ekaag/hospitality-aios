using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Dining.Restaurants.Events;

/// <summary>
/// A restaurant was created (spec §8).
/// </summary>
public sealed record RestaurantCreated(RestaurantId RestaurantId, PropertyId PropertyId, RestaurantDetails Details, DateTimeOffset OccurredAt) : IDomainEvent;
