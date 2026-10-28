using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Ports.Search;

/// <summary>
/// Keyword search over documents (plan §2.1), e.g. a property's published content. An index is a projection of an
/// authoritative store: callers re-read hits from that store before using them. Every document belongs to one tenant and
/// one scope (e.g. a property); a search sees only its own tenant's documents in one scope (P10). Bound through
/// <c>Bindings:SearchIndex</c>; instances are shared, so adapters must be thread-safe.
/// </summary>
public interface ISearchIndex
{
    /// <summary>
    /// Adds the document, or replaces the one with the same tenant and ID.
    /// </summary>
    /// <param name="index">The index, e.g. <c>content</c>.</param>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task UpsertAsync(string index, SearchDocument document, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the tenant's document with this ID, if there is one.
    /// </summary>
    Task DeleteAsync(string index, TenantId tenantId, string id, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every document of the tenant in <paramref name="scope"/>, e.g. before rebuilding it.
    /// </summary>
    Task DeleteScopeAsync(string index, TenantId tenantId, string scope, CancellationToken cancellationToken);

    /// <summary>
    /// The documents matching the query's words, best first; only matches (score above zero).
    /// </summary>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string index, KeywordQuery query, CancellationToken cancellationToken);
}
