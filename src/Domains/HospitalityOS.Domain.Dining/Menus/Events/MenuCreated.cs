using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Dining.Menus.Events;

/// <summary>
/// A menu was created for a restaurant.
/// </summary>
public sealed record MenuCreated(
    MenuId MenuId,
    PropertyId PropertyId,
    RestaurantId RestaurantId,
    MenuDetails Details,
    DateTimeOffset OccurredAt) : IDomainEvent;
