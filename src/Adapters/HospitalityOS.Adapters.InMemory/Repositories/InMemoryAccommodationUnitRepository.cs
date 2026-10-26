using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryAccommodationUnitRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<AccommodationUnit, AccommodationUnitId>(store, unitOfWork, currentTenant), IAccommodationUnitRepository
{
    public Task<AccommodationUnit?> FindByUnitNumberAsync(PropertyId propertyId, string unitNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitNumber);

        string number = unitNumber.Trim();

        return FindFirstAsync(
            unit => unit.PropertyId == propertyId && string.Equals(unit.UnitNumber, number, StringComparison.OrdinalIgnoreCase),
            cancellationToken);
    }
}
