using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryPropertyEventRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<PropertyEvent, PropertyEventId>(store, unitOfWork, currentTenant), IPropertyEventRepository;
