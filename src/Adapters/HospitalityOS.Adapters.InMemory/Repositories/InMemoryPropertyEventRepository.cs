using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryPropertyEventRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<PropertyEvent, PropertyEventId>(store, unitOfWork, currentTenant), IPropertyEventRepository
{
    public Task<IReadOnlyList<PropertyEvent>> ListScheduledEndingAfterAsync(PropertyId propertyId, DateTime localTime, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(propertyEvent => propertyEvent.PropertyId == propertyId && propertyEvent.Status == PropertyEventStatus.Scheduled && propertyEvent.Details.LocalEnd > localTime), cancellationToken);
}
