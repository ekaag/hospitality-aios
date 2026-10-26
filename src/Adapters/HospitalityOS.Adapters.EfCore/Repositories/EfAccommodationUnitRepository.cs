using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfAccommodationUnitRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<AccommodationUnit, AccommodationUnitId>(context, unitOfWork, currentTenant), IAccommodationUnitRepository
{
    public Task<AccommodationUnit?> FindByUnitNumberAsync(PropertyId propertyId, string unitNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitNumber);

        string key = KeyColumns.UnitNumber(unitNumber);

        return FindFirstAsync(
            unit => unit.PropertyId == propertyId && EF.Property<string>(unit, KeyColumns.UnitNumberKey) == key,
            cancellationToken);
    }
}
