using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfRestaurantRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Restaurant, RestaurantId>(context, unitOfWork, currentTenant), IRestaurantRepository
{
    public Task<IReadOnlyList<Restaurant>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(restaurant => restaurant.PropertyId == propertyId), cancellationToken);
}
