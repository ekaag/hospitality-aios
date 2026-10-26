using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryAccommodationTypeRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<AccommodationType, AccommodationTypeId>(store, unitOfWork, currentTenant), IAccommodationTypeRepository;
