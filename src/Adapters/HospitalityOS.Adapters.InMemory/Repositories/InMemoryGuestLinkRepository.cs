using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Guests.GuestLinks;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryGuestLinkRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<GuestLink, GuestLinkId>(store, unitOfWork, currentTenant), IGuestLinkRepository
{
    public Task<GuestLink?> FindOpenForStayAsync(StayContextId stayContextId, CancellationToken cancellationToken) =>
        FindFirstAsync(link => link.StayContextId == stayContextId && link.RevokedAt == null, cancellationToken);

    public Task<IReadOnlyList<GuestLink>> ListOpenForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(links => links.Where(link => link.PropertyId == propertyId && link.RevokedAt == null), cancellationToken);
}
