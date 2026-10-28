using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.Retrieval;

/// <summary>
/// Keeps the content search index and vector store in step with the content repository, the authoritative store:
/// published items are indexed and embedded, anything else is removed. Content events drive it (<c>AddContentIndexing</c>);
/// <see cref="RebuildAsync"/> fills an empty index, e.g. after an in-memory index restarts over durable content. Runs for
/// the scope's tenant; scoped.
/// </summary>
public interface IContentIndexer
{
    /// <summary>
    /// Indexes the item as it is now in the repository, or removes it when it is not published or no longer exists.
    /// </summary>
    /// <returns>Success, or <c>inference.unavailable</c> when it could not be embedded (retry later).</returns>
    Task<Result> IndexAsync(ContentItemId id, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the property's entries and indexes all of its published content again.
    /// </summary>
    /// <returns>How many items were indexed, or <c>inference.unavailable</c>.</returns>
    Task<Result<int>> RebuildAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
