using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfRestaurantRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Restaurant, RestaurantId>(context, unitOfWork, currentTenant), IRestaurantRepository;
