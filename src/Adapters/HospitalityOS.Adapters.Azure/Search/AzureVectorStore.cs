using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using HospitalityOS.Ports.Search;
using HospitalityOS.SharedKernel.Tenancy;
using Document = Azure.Search.Documents.Models.SearchDocument;
using VectorQuery = HospitalityOS.Ports.Search.VectorQuery;

namespace HospitalityOS.Adapters.Azure.Search;

/// <summary>
/// <see cref="IVectorStore"/> on Azure AI Search: a collection is the deployed index <c>{collection}-vectors</c>, whose
/// vector field uses cosine similarity with the embedding model's width. Searches filter before ranking, so a tenant's
/// nearest records are found among its own (P10). Azure scores cosine matches as <c>1 / (2 - cos)</c>; hits are given back
/// as the cosine similarity itself, as the port promises.
/// </summary>
internal sealed class AzureVectorStore(AzureSearchIndexes indexes) : IVectorStore
{
    private const int PageSize = 1000;

    public Task UpsertAsync(string collection, VectorRecord record, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(record);

        Document indexed = new()
        {
            [AzureSearchIndexes.Key] = AzureSearchIndexes.KeyOf(record.TenantId, record.Id),
            [AzureSearchIndexes.Tenant] = record.TenantId.Value.ToString("D"),
            [AzureSearchIndexes.Scope] = record.Scope,
            [AzureSearchIndexes.Kind] = record.Kind,
            [AzureSearchIndexes.Id] = record.Id,
            [AzureSearchIndexes.Vector] = record.Vector.ToArray(),
        };

        return AzureSearchIndexes.OnDeployedIndexAsync(Index(collection), () =>
            Client(collection).UploadDocumentsAsync([indexed], new IndexDocumentsOptions { ThrowOnAnyError = true }, cancellationToken));
    }

    public Task DeleteAsync(string collection, TenantId tenantId, string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(id);

        return AzureSearchIndexes.OnDeployedIndexAsync(Index(collection), () =>
            Client(collection).DeleteDocumentsAsync(AzureSearchIndexes.Key, [AzureSearchIndexes.KeyOf(tenantId, id)], cancellationToken: cancellationToken));
    }

    public Task DeleteScopeAsync(string collection, TenantId tenantId, string scope, CancellationToken cancellationToken) =>
        AzureSearchScopes.DeleteAsync(Client(collection), Index(collection), AzureSearchIndexes.Filter(tenantId, scope, null), PageSize, cancellationToken);

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string collection, VectorQuery query, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Top, 1);

        VectorizedQuery nearest = new(query.Vector) { KNearestNeighborsCount = query.Top };
        nearest.Fields.Add(AzureSearchIndexes.Vector);
        SearchOptions options = new()
        {
            Filter = AzureSearchIndexes.Filter(query.TenantId, query.Scope, query.Kind),
            Size = query.Top,
            VectorSearch = new VectorSearchOptions { FilterMode = VectorFilterMode.PreFilter },
        };
        options.VectorSearch.Queries.Add(nearest);
        options.Select.Add(AzureSearchIndexes.Id);

        Response<SearchResults<Document>> response = await AzureSearchIndexes.OnDeployedIndexAsync(Index(collection), () =>
            Client(collection).SearchAsync<Document>(null, options, cancellationToken)).ConfigureAwait(false);
        List<SearchHit> hits = [];

        await foreach (SearchResult<Document> result in response.Value.GetResultsAsync().ConfigureAwait(false))
        {
            if (result.Score is { } score && Cosine(score) is var similarity && similarity >= query.MinScore)
            {
                hits.Add(new SearchHit(result.Document.GetString(AzureSearchIndexes.Id), similarity));
            }
        }

        return [.. hits.OrderByDescending(hit => hit.Score).ThenBy(hit => hit.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The cosine similarity behind an Azure cosine score: score = 1 / (1 + (1 - cos)).
    /// </summary>
    internal static double Cosine(double score) => Math.Clamp(2 - (1 / score), -1, 1);

    private static string Index(string collection) => collection + AzureSearchIndexes.VectorSuffix;

    private SearchClient Client(string collection) => indexes.Client(Index(collection));
}
