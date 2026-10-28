using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Ports.Search;

/// <summary>
/// Semantic retrieval by vector similarity (plan §2.1), over embeddings an inference task produced. Like
/// <see cref="ISearchIndex"/>, a projection of an authoritative store, partitioned by tenant and scope (P10). Bound
/// through <c>Bindings:VectorStore</c>; instances are shared, so adapters must be thread-safe.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Adds the record, or replaces the one with the same tenant and ID.
    /// </summary>
    /// <param name="collection">The collection, e.g. <c>content</c>.</param>
    /// <param name="record">The record.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task UpsertAsync(string collection, VectorRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the tenant's record with this ID, if there is one.
    /// </summary>
    Task DeleteAsync(string collection, TenantId tenantId, string id, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every record of the tenant in <paramref name="scope"/>, e.g. before rebuilding it.
    /// </summary>
    Task DeleteScopeAsync(string collection, TenantId tenantId, string scope, CancellationToken cancellationToken);

    /// <summary>
    /// The records most similar to the query's vector (cosine similarity, -1 to 1), best first, at least
    /// <see cref="VectorQuery.MinScore"/>.
    /// </summary>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string collection, VectorQuery query, CancellationToken cancellationToken);
}
