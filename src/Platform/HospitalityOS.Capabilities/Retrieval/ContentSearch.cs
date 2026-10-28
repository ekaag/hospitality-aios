using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Search;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Capabilities.Retrieval;

/// <summary>
/// Hybrid content search: keyword hits (<see cref="ISearchIndex"/>) and semantic hits (<see cref="IVectorStore"/>, the
/// query embedded by <c>content.embed</c>) merged by reciprocal rank fusion, then re-read from the repository so only
/// content published now is returned (an index may lag or hold stale entries). If the query cannot be embedded, keyword
/// hits alone are used: a read never fails because a model is down.
/// </summary>
internal sealed class ContentSearch(
    IContentItemRepository content,
    IBindingResolver<ISearchIndex> searchIndexes,
    IBindingResolver<IVectorStore> vectorStores,
    IInferenceRouter router,
    TenantContext tenant,
    IOptions<ContentSearchOptions> options)
{
    /// <summary>
    /// Reciprocal rank fusion's damping constant: a hit's score is the sum of 1 / (60 + rank) over the lists it appears in.
    /// </summary>
    public const int FusionConstant = 60;

    public async Task<IReadOnlyList<ContentItem>> SearchAsync(PropertyId propertyId, string query, ContentType? type, int limit, CancellationToken cancellationToken)
    {
        TenantId tenantId = tenant.TenantId;
        string scope = propertyId.Value.ToString();
        string? kind = type?.ToString();
        int candidates = limit * 2;

        IReadOnlyList<SearchHit> keyword = await searchIndexes.Resolve(ContentIndex.Binding(tenantId))
            .SearchAsync(ContentIndex.Name, new KeywordQuery(tenantId, scope, query, kind, candidates), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<SearchHit> semantic = await SemanticAsync(tenantId, scope, query, kind, candidates, cancellationToken).ConfigureAwait(false);

        string[] ranked = [.. Fuse(keyword, semantic)];

        if (ranked.Length == 0)
        {
            return [];
        }

        Dictionary<string, ContentItem> published = (await content.ListPublishedForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false))
            .ToDictionary(item => item.Id.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        return [.. ranked
            .Select(id => published.GetValueOrDefault(id))
            .OfType<ContentItem>()
            .Where(item => type is null || item.Details.Type == type)
            .Take(limit)];
    }

    /// <summary>
    /// IDs by fused score, best first; ties by ID so results are stable.
    /// </summary>
    internal static IEnumerable<string> Fuse(params IReadOnlyList<SearchHit>[] lists) =>
        lists
            .SelectMany(list => list.Select((hit, rank) => (hit.Id, Score: 1.0 / (FusionConstant + rank + 1))))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => (Id: group.Key, Score: group.Sum(entry => entry.Score)))
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(entry => entry.Id);

    private async Task<IReadOnlyList<SearchHit>> SemanticAsync(TenantId tenantId, string scope, string query, string? kind, int top, CancellationToken cancellationToken)
    {
        Result<InferenceOutcome<Embeddings>> embedded = await router.RunAsync<IEmbeddingModel, Embeddings>(
            ContentIndex.EmbeddingTask,
            (model, selection, _, token) => model.EmbedAsync(new EmbeddingRequest(ValueList.From([query])), selection, token),
            cancellationToken).ConfigureAwait(false);

        if (embedded.IsFailure)
        {
            return [];
        }

        return await vectorStores.Resolve(ContentIndex.Binding(tenantId))
            .SearchAsync(ContentIndex.Name, new VectorQuery(tenantId, scope, embedded.Value.Value.Vectors[0], kind, top, options.Value.MinSimilarity), cancellationToken)
            .ConfigureAwait(false);
    }
}
