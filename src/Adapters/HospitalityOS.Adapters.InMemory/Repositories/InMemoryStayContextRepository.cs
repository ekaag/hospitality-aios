using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryStayContextRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<StayContext, StayContextId>(store, unitOfWork, currentTenant), IStayContextRepository
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
