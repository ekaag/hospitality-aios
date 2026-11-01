using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Document = Azure.Search.Documents.Models.SearchDocument;

namespace HospitalityOS.Adapters.Azure.Search;

/// <summary>
/// Deletes every document a filter matches, a page at a time. The index catches up with deletions about a second later,
/// so a page that holds only keys already deleted means the scope is empty.
/// </summary>
internal static class AzureSearchScopes
{
    public static Task DeleteAsync(SearchClient client, string index, string filter, int pageSize, CancellationToken cancellationToken) =>
        AzureSearchIndexes.OnDeployedIndexAsync(index, async () =>
        {
            HashSet<string> deleted = new(StringComparer.Ordinal);

            while (true)
            {
                SearchOptions options = new() { Filter = filter, Size = pageSize };
                options.Select.Add(AzureSearchIndexes.Key);
                Response<SearchResults<Document>> response = await client.SearchAsync<Document>("*", options, cancellationToken).ConfigureAwait(false);
                List<string> keys = [];

                await foreach (SearchResult<Document> result in response.Value.GetResultsAsync().ConfigureAwait(false))
                {
                    string key = result.Document.GetString(AzureSearchIndexes.Key);

                    if (deleted.Add(key))
                    {
                        keys.Add(key);
                    }
                }

                if (keys.Count == 0)
                {
                    return true;
                }

                await client.DeleteDocumentsAsync(AzureSearchIndexes.Key, keys, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        });
}
