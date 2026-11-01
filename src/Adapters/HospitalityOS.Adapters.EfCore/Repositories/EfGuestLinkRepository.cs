using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Guests.GuestLinks;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfGuestLinkRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<GuestLink, GuestLinkId>(context, unitOfWork, currentTenant), IGuestLinkRepository
{
    public Task<GuestLink?> FindOpenForStayAsync(StayContextId stayContextId, CancellationToken cancellationToken) =>
        FindFirstAsync(link => link.StayContextId == stayContextId && link.RevokedAt == null, cancellationToken);

    public Task<IReadOnlyList<GuestLink>> ListOpenForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(links => links.Where(link => link.PropertyId == propertyId && link.RevokedAt == null), cancellationToken);
}
