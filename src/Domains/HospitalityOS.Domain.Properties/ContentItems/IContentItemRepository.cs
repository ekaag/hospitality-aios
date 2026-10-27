using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.ContentItems;

/// <summary>
/// Persistence port for <see cref="ContentItem"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work. Content search goes through <c>ISearchIndex</c>/<c>IVectorStore</c> (G5).
/// </summary>
public interface IContentItemRepository
{
    /// <summary>
    /// Loads a content item, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<ContentItem?> GetByIdAsync(ContentItemId id, CancellationToken cancellationToken);

    /// <summary>
    /// The property's published content, in no particular order.
    /// </summary>
    Task<IReadOnlyList<ContentItem>> ListPublishedForPropertyAsync(PropertyId propertyId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new content item, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(ContentItem contentItem, CancellationToken cancellationToken);
}
