using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryPolicyRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Policy, PolicyId>(store, unitOfWork, currentTenant), IPolicyRepository;
