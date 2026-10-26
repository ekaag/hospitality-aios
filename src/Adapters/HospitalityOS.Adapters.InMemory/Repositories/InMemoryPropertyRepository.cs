using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryPropertyRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Property, PropertyId>(store, unitOfWork, currentTenant), IPropertyRepository;
