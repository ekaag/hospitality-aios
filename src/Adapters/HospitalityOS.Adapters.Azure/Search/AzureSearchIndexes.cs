using System.Collections.Concurrent;
using System.Text;
using Azure;
using Azure.Core;
using Azure.Search.Documents;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.Azure.Search;

/// <summary>
/// What the Azure AI Search adapters share: one service, a client per index, the field names the index definitions in
/// <c>deploy/azure/search/</c> declare, and how a tenant's document is keyed and filtered (P10). Indexes are deployed,
/// not created here: the app's identity may read and write documents only.
/// </summary>
internal sealed class AzureSearchIndexes(Uri endpoint, TokenCredential credential, string prefix)
{
    public const string Key = "key";
    public const string Tenant = "tenantId";
    public const string Scope = "scope";
    public const string Kind = "kind";
    public const string Id = "id";
    public const string Title = "title";
    public const string Body = "body";
    public const string Tags = "tags";
    public const string Vector = "vector";

    /// <summary>
    /// The suffix of a vector collection's index: the keyword index <c>content</c> and the vectors <c>content-vectors</c>.
    /// </summary>
    public const string VectorSuffix = "-vectors";

    private readonly ConcurrentDictionary<string, SearchClient> clients = new(StringComparer.Ordinal);

    /// <summary>
    /// The client for a deployed index, by its logical name.
    /// </summary>
    public SearchClient Client(string index) =>
        clients.GetOrAdd(prefix + index, name => new SearchClient(endpoint, name, credential));

    /// <summary>
    /// The document key: unique per tenant and ID, in the characters keys allow (letters, digits, <c>-</c>, <c>_</c>).
    /// </summary>
    public static string KeyOf(TenantId tenantId, string id) =>
        $"{tenantId.Value:N}_{Convert.ToBase64String(Encoding.UTF8.GetBytes(id)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";

    /// <summary>
    /// Only the tenant's documents in one scope, and of one kind when given.
    /// </summary>
    public static string Filter(TenantId tenantId, string scope, string? kind) =>
        $"{Tenant} eq '{tenantId.Value:D}' and {Scope} eq '{Quote(scope)}'" + (kind is null ? string.Empty : $" and {Kind} eq '{Quote(kind)}'");

    /// <summary>
    /// Runs a call against an index, explaining a missing index as one nobody deployed.
    /// </summary>
    public static async Task<T> OnDeployedIndexAsync<T>(string index, Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new InvalidOperationException(
                $"The search index '{index}' is not deployed. Indexes are infrastructure: add its definition to deploy/azure/search/ and deploy.",
                exception);
        }
    }

    private static string Quote(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
