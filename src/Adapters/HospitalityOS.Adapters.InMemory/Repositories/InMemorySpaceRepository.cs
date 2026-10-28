using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemorySpaceRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Space, SpaceId>(store, unitOfWork, currentTenant), ISpaceRepository
{
    public Task<IReadOnlyList<Space>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(space => space.PropertyId == propertyId), cancellationToken);
}
