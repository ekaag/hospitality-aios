using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfAmenityRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<Amenity, AmenityId>(context, unitOfWork, currentTenant), IAmenityRepository
{
    public Task<IReadOnlyList<Amenity>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(amenity => amenity.PropertyId == propertyId), cancellationToken);
}
