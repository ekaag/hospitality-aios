namespace HospitalityOS.Domain.Dining.Restaurants;

/// <summary>
/// Persistence port for <see cref="Restaurant"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work. Restaurant search for answers goes through read-side queries (D3).
/// </summary>
public interface IRestaurantRepository
{
    /// <summary>
    /// Loads a restaurant, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<Restaurant?> GetByIdAsync(RestaurantId id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new restaurant, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(Restaurant restaurant, CancellationToken cancellationToken);
}
