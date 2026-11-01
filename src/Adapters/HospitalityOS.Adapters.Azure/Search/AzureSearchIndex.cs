using System.Text.RegularExpressions;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using HospitalityOS.Ports.Search;
using HospitalityOS.SharedKernel.Tenancy;
using Document = Azure.Search.Documents.Models.SearchDocument;
using SearchDocument = HospitalityOS.Ports.Search.SearchDocument;

namespace HospitalityOS.Adapters.Azure.Search;

/// <summary>
/// <see cref="ISearchIndex"/> on Azure AI Search: each logical index is a deployed index whose analyzer drops hyphens
/// ("wifi" finds "Wi-Fi") and common English words, and whose default scoring profile weighs title and tag words above
/// body words, as the in-memory index does. Guest text is searched as plain words: query operators are taken out.
/// Writes are searchable about a second later (near real time).
/// </summary>
internal sealed partial class AzureSearchIndex(AzureSearchIndexes indexes) : ISearchIndex
{
    /// <summary>
    /// How many documents a scope deletion handles per round.
    /// </summary>
    private const int PageSize = 1000;

    public Task UpsertAsync(string index, SearchDocument document, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);
        ArgumentNullException.ThrowIfNull(document);

        Document indexed = new()
        {
            [AzureSearchIndexes.Key] = AzureSearchIndexes.KeyOf(document.TenantId, document.Id),
            [AzureSearchIndexes.Tenant] = document.TenantId.Value.ToString("D"),
            [AzureSearchIndexes.Scope] = document.Scope,
            [AzureSearchIndexes.Kind] = document.Kind,
            [AzureSearchIndexes.Id] = document.Id,
            [AzureSearchIndexes.Title] = document.Title,
            [AzureSearchIndexes.Body] = document.Body,
            [AzureSearchIndexes.Tags] = document.Tags.ToArray(),
        };

        return AzureSearchIndexes.OnDeployedIndexAsync(index, () =>
            indexes.Client(index).UploadDocumentsAsync([indexed], new IndexDocumentsOptions { ThrowOnAnyError = true }, cancellationToken));
    }

    public Task DeleteAsync(string index, TenantId tenantId, string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);
        ArgumentNullException.ThrowIfNull(id);

        return AzureSearchIndexes.OnDeployedIndexAsync(index, () =>
            indexes.Client(index).DeleteDocumentsAsync(AzureSearchIndexes.Key, [AzureSearchIndexes.KeyOf(tenantId, id)], cancellationToken: cancellationToken));
    }

    public Task DeleteScopeAsync(string index, TenantId tenantId, string scope, CancellationToken cancellationToken) =>
        AzureSearchScopes.DeleteAsync(indexes.Client(index), index, AzureSearchIndexes.Filter(tenantId, scope, null), PageSize, cancellationToken);

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string index, KeywordQuery query, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Top, 1);

        string text = Words(query.Text);

        if (text.Length == 0)
        {
            return [];
        }

        SearchOptions options = new()
        {
            Filter = AzureSearchIndexes.Filter(query.TenantId, query.Scope, query.Kind),
            Size = query.Top,
            SearchMode = SearchMode.Any,
            QueryType = SearchQueryType.Simple,
        };
        options.Select.Add(AzureSearchIndexes.Id);

        Response<SearchResults<Document>> response = await AzureSearchIndexes.OnDeployedIndexAsync(index, () =>
            indexes.Client(index).SearchAsync<Document>(text, options, cancellationToken)).ConfigureAwait(false);
        List<SearchHit> hits = [];

        await foreach (SearchResult<Document> result in response.Value.GetResultsAsync().ConfigureAwait(false))
        {
            if (result.Score is > 0 and { } score)
            {
                hits.Add(new SearchHit(result.Document.GetString(AzureSearchIndexes.Id), score));
            }
        }

        return [.. hits.OrderByDescending(hit => hit.Score).ThenBy(hit => hit.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The guest's words without simple-query operators, and without hyphens, as the analyzer indexes them.
    /// </summary>
    internal static string Words(string text) =>
        Operators().Replace(text.Replace("-", string.Empty, StringComparison.Ordinal), " ").Trim();

    [GeneratedRegex("[+|\"()*~\\\\/&!{}\\[\\]^:?]+", RegexOptions.CultureInvariant)]
    private static partial Regex Operators();
}
