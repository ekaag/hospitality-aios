using HospitalityOS.Capabilities.Retrieval;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Properties;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai.Retrieval;

/// <summary>
/// Keeps content search current: each content event re-indexes the item as it is now (published) or removes it. An
/// embedding outage throws, so the event is redelivered later.
/// </summary>
internal sealed class ContentIndexingHandler(IContentIndexer indexer) :
    IEventHandler<ContentItemCreatedV1>,
    IEventHandler<ContentItemUpdatedV1>
{
    public const string OnCreated = "Content.IndexOnContentItemCreated";
    public const string OnUpdated = "Content.IndexOnContentItemUpdated";

    public Task HandleAsync(EventEnvelope envelope, ContentItemCreatedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return IndexAsync(payload.ContentItemId, cancellationToken);
    }

    public Task HandleAsync(EventEnvelope envelope, ContentItemUpdatedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return IndexAsync(payload.ContentItemId, cancellationToken);
    }

    private async Task IndexAsync(Guid id, CancellationToken cancellationToken)
    {
        Result indexed = await indexer.IndexAsync(new ContentItemId(id), cancellationToken).ConfigureAwait(false);

        if (indexed.IsFailure)
        {
            throw new InvalidOperationException($"Content item {id} could not be indexed: {indexed.Error.Message}");
        }
    }
}
