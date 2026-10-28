using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryContentItemRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<ContentItem, ContentItemId>(store, unitOfWork, currentTenant), IContentItemRepository
{
    public Task<IReadOnlyList<ContentItem>> ListForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(item => item.PropertyId == propertyId), cancellationToken);

    public Task<IReadOnlyList<ContentItem>> ListPublishedForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(item => item.PropertyId == propertyId && item.Status == ContentStatus.Published), cancellationToken);
}
