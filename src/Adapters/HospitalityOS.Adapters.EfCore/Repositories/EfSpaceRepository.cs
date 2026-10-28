using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfSpaceRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Space, SpaceId>(context, unitOfWork, currentTenant), ISpaceRepository
{
    public Task<IReadOnlyList<Space>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(space => space.PropertyId == propertyId), cancellationToken);
}
