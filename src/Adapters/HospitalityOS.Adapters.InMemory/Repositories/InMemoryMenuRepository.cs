using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryMenuRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Menu, MenuId>(store, unitOfWork, currentTenant), IMenuRepository;
