using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryPolicyRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Policy, PolicyId>(store, unitOfWork, currentTenant), IPolicyRepository
{
    public Task<IReadOnlyList<Policy>> ListActiveForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(policy => policy.PropertyId == propertyId && policy.Status == PolicyStatus.Active), cancellationToken);
}
