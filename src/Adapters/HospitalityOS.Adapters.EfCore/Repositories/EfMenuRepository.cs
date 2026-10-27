using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfMenuRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Menu, MenuId>(context, unitOfWork, currentTenant), IMenuRepository
{
    public Task<IReadOnlyList<Menu>> ListForRestaurantAsync(RestaurantId restaurantId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(menu => menu.RestaurantId == restaurantId), cancellationToken);

    public Task<IReadOnlyList<Menu>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(menu => menu.PropertyId == propertyId), cancellationToken);
}
