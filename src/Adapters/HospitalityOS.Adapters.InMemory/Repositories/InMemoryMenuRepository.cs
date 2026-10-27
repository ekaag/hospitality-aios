using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryMenuRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Menu, MenuId>(store, unitOfWork, currentTenant), IMenuRepository
{
    public Task<IReadOnlyList<Menu>> ListForRestaurantAsync(RestaurantId restaurantId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(menu => menu.RestaurantId == restaurantId), cancellationToken);

    public Task<IReadOnlyList<Menu>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(menu => menu.PropertyId == propertyId), cancellationToken);
}
