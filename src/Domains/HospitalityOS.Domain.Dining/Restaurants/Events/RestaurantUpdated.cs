using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Dining.Restaurants.Events;

/// <summary>
/// A restaurant's descriptive information changed (spec §8). Carries the full new details, not a diff.
/// </summary>
public sealed record RestaurantUpdated(RestaurantId RestaurantId, RestaurantDetails Details, DateTimeOffset OccurredAt) : IDomainEvent;
