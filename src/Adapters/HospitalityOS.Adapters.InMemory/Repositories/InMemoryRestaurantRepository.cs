using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryRestaurantRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Restaurant, RestaurantId>(store, unitOfWork, currentTenant), IRestaurantRepository
{
    public Task<IReadOnlyList<Restaurant>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(restaurant => restaurant.PropertyId == propertyId), cancellationToken);
}
