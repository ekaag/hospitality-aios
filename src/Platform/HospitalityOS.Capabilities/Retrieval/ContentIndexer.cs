using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Search;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Capabilities.Retrieval;

/// <summary>
/// Indexes content from the repository (see <see cref="IContentIndexer"/>). Reading the item's current state rather than
/// an event's payload makes indexing order-independent: a late or replayed event re-indexes what is true now.
/// </summary>
internal sealed class ContentIndexer(
    IContentItemRepository content,
    IBindingResolver<ISearchIndex> searchIndexes,
    IBindingResolver<IVectorStore> vectorStores,
    IInferenceRouter router,
    TenantContext tenant) : IContentIndexer
{
    public async Task<Result> IndexAsync(ContentItemId id, CancellationToken cancellationToken)
    {
        ContentItem? item = await content.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (item is not { Status: ContentStatus.Published })
        {
            TenantId tenantId = tenant.TenantId;
            await searchIndexes.Resolve(ContentIndex.Binding(tenantId)).DeleteAsync(ContentIndex.Name, tenantId, id.Value.ToString(), cancellationToken).ConfigureAwait(false);
            await vectorStores.Resolve(ContentIndex.Binding(tenantId)).DeleteAsync(ContentIndex.Name, tenantId, id.Value.ToString(), cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        return await IndexAsync([item], cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<int>> RebuildAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        TenantId tenantId = tenant.TenantId;
        string scope = propertyId.Value.ToString();
        IReadOnlyList<ContentItem> published = await content.ListPublishedForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        await searchIndexes.Resolve(ContentIndex.Binding(tenantId)).DeleteScopeAsync(ContentIndex.Name, tenantId, scope, cancellationToken).ConfigureAwait(false);
        await vectorStores.Resolve(ContentIndex.Binding(tenantId)).DeleteScopeAsync(ContentIndex.Name, tenantId, scope, cancellationToken).ConfigureAwait(false);

        if (published.Count == 0)
        {
            return 0;
        }

        Result indexed = await IndexAsync(published, cancellationToken).ConfigureAwait(false);

        return indexed.IsSuccess ? published.Count : indexed.Error;
    }

    // Embeds first, so an outage leaves the index as it was rather than half updated.
    private async Task<Result> IndexAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken)
    {
        Result<InferenceOutcome<Embeddings>> embedded = await router.RunAsync<IEmbeddingModel, Embeddings>(
            ContentIndex.EmbeddingTask,
            (model, selection, _, token) => model.EmbedAsync(new EmbeddingRequest(ValueList.From(items.Select(item => ContentIndex.Text(item.Details)))), selection, token),
            cancellationToken).ConfigureAwait(false);

        if (embedded.IsFailure)
        {
            return embedded.Error;
        }

        for (int i = 0; i < items.Count; i++)
        {
            ContentItem item = items[i];
            string kind = item.Details.Type.ToString();
            ISearchIndex index = searchIndexes.Resolve(ContentIndex.Binding(item.TenantId));
            IVectorStore vectors = vectorStores.Resolve(ContentIndex.Binding(item.TenantId));

            await index.UpsertAsync(
                ContentIndex.Name,
                new SearchDocument(item.Id.Value.ToString(), item.TenantId, ContentIndex.Scope(item), kind, item.Details.Title, item.Details.Body, item.Details.Tags ?? []),
                cancellationToken).ConfigureAwait(false);
            await vectors.UpsertAsync(
                ContentIndex.Name,
                new VectorRecord(item.Id.Value.ToString(), item.TenantId, ContentIndex.Scope(item), kind, embedded.Value.Value.Vectors[i]),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}
