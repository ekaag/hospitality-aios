using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryOrganizationRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Organization, OrganizationId>(store, unitOfWork, currentTenant), IOrganizationRepository
{
    public Task<IReadOnlyList<Organization>> ListAsync(CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates, cancellationToken);
}
