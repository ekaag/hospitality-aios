using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryRestaurantRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Restaurant, RestaurantId>(store, unitOfWork, currentTenant), IRestaurantRepository;
