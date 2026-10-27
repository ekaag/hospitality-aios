using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfStayContextRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<StayContext, StayContextId>(context, unitOfWork, currentTenant), IStayContextRepository
{
    public Task<StayContext?> FindInHouseAsync(PropertyId propertyId, GuestId guestId, CancellationToken cancellationToken)
    {
        return FindFirstAsync(
            stay => stay.PropertyId == propertyId && stay.GuestId == guestId && stay.Status == StayStatus.InHouse,
            cancellationToken);
    }

    public Task<StayContext?> FindInHouseInUnitAsync(PropertyId propertyId, AccommodationUnitId unitId, CancellationToken cancellationToken)
    {
        return FindFirstAsync(
            stay => stay.PropertyId == propertyId && stay.AccommodationUnitId == unitId && stay.Status == StayStatus.InHouse,
            cancellationToken);
    }
}
