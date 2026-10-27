using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfPropertyEventRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<PropertyEvent, PropertyEventId>(context, unitOfWork, currentTenant), IPropertyEventRepository
{
    public Task<IReadOnlyList<PropertyEvent>> ListScheduledEndingAfterAsync(PropertyId propertyId, DateTime localTime, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(propertyEvent => propertyEvent.PropertyId == propertyId && propertyEvent.Status == PropertyEventStatus.Scheduled && propertyEvent.Details.LocalEnd > localTime), cancellationToken);
}
