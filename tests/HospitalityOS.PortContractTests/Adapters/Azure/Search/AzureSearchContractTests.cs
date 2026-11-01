using HospitalityOS.PortContractTests.Search;
using HospitalityOS.Ports.Search;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.PortContractTests.Adapters.Azure.Search;

/// <summary>
/// The Azure AI Search keyword index against a real service, on indexes of its own (see <see cref="AzureSearchTestService"/>).
/// </summary>
public sealed class AzureSearchIndexContractTests : SearchIndexContractTests, IAsyncLifetime
{
    private readonly AzureSearchTestService service = new();

    public ValueTask InitializeAsync() => new(service.CreateAsync(Cancellation));

    public ValueTask DisposeAsync() => new(service.DeleteAsync());

    protected override ISearchIndex CreateIndex()
    {
        service.SkipUnlessConfigured();

        return new Settled(service.Adapter<ISearchIndex>());
    }

    /// <summary>
    /// Waits after each write until it is searchable, as Azure indexes in near real time.
    /// </summary>
    private sealed class Settled(ISearchIndex inner) : ISearchIndex
    {
        public async Task UpsertAsync(string index, SearchDocument document, CancellationToken cancellationToken)
        {
            await inner.UpsertAsync(index, document, cancellationToken);
            await Task.Delay(AzureSearchTestService.Settle, cancellationToken);
        }

        public async Task DeleteAsync(string index, TenantId tenantId, string id, CancellationToken cancellationToken)
        {
            await inner.DeleteAsync(index, tenantId, id, cancellationToken);
            await Task.Delay(AzureSearchTestService.Settle, cancellationToken);
        }

        public async Task DeleteScopeAsync(string index, TenantId tenantId, string scope, CancellationToken cancellationToken)
        {
            await inner.DeleteScopeAsync(index, tenantId, scope, cancellationToken);
            await Task.Delay(AzureSearchTestService.Settle, cancellationToken);
        }

        public Task<IReadOnlyList<SearchHit>> SearchAsync(string index, KeywordQuery query, CancellationToken cancellationToken) =>
            inner.SearchAsync(index, query, cancellationToken);
    }
}

/// <summary>
/// The Azure AI Search vector store against a real service, on indexes of its own (see <see cref="AzureSearchTestService"/>).
/// </summary>
public sealed class AzureVectorStoreContractTests : VectorStoreContractTests, IAsyncLifetime
{
    private readonly AzureSearchTestService service = new();

    public ValueTask InitializeAsync() => new(service.CreateAsync(Cancellation));

    public ValueTask DisposeAsync() => new(service.DeleteAsync());

    protected override IVectorStore CreateStore()
    {
        service.SkipUnlessConfigured();

        return new Settled(service.Adapter<IVectorStore>());
    }

    /// <summary>
    /// Waits after each write until it is searchable, as Azure indexes in near real time.
    /// </summary>
    private sealed class Settled(IVectorStore inner) : IVectorStore
    {
        public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken cancellationToken)
        {
            await inner.UpsertAsync(collection, record, cancellationToken);
            await Task.Delay(AzureSearchTestService.Settle, cancellationToken);
        }

        public async Task DeleteAsync(string collection, TenantId tenantId, string id, CancellationToken cancellationToken)
        {
            await inner.DeleteAsync(collection, tenantId, id, cancellationToken);
            await Task.Delay(AzureSearchTestService.Settle, cancellationToken);
        }

        public async Task DeleteScopeAsync(string collection, TenantId tenantId, string scope, CancellationToken cancellationToken)
        {
            await inner.DeleteScopeAsync(collection, tenantId, scope, cancellationToken);
            await Task.Delay(AzureSearchTestService.Settle, cancellationToken);
        }

        public Task<IReadOnlyList<SearchHit>> SearchAsync(string collection, VectorQuery query, CancellationToken cancellationToken) =>
            inner.SearchAsync(collection, query, cancellationToken);
    }
}
