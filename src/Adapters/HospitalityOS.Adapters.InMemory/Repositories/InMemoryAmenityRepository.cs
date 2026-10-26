using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryAmenityRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Amenity, AmenityId>(store, unitOfWork, currentTenant), IAmenityRepository;
