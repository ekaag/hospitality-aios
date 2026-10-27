using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryAmenityRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<Amenity, AmenityId>(store, unitOfWork, currentTenant), IAmenityRepository
{
    public Task<IReadOnlyList<Amenity>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(amenity => amenity.PropertyId == propertyId), cancellationToken);
}
