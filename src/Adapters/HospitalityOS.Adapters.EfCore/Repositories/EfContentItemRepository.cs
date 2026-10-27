using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfContentItemRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<ContentItem, ContentItemId>(context, unitOfWork, currentTenant), IContentItemRepository
{
    public Task<IReadOnlyList<ContentItem>> ListPublishedForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
        QueryAsync(aggregates => aggregates.Where(item => item.PropertyId == propertyId && item.Status == ContentStatus.Published), cancellationToken);
}
