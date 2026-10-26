using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Eventing.Replay;

/// <summary>
/// Pages through the event store and publishes each matching event straight to the transport (not through the
/// outbox: it is already recorded). Singleton.
/// </summary>
internal sealed partial class EventReplayer(
    Func<ReplayScope> createScope,
    IBindingResolver<IEventTransport> transports,
    ILogger<EventReplayer> logger) : IEventReplayer
{
    private const int PageSize = 500;

    public async Task<int> ReplayAsync(EventReplayRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.AfterPosition);

        IEventTransport transport = transports.Resolve();
        await using ReplayScope scope = createScope();
        long after = request.AfterPosition;
        int published = 0;

        while (true)
        {
            EventStoreQuery page = new(after, PageSize, request.TenantId, request.CorrelationId, request.RecordedFrom, request.RecordedBefore);
            IReadOnlyList<StoredEvent> events = await scope.EventStore.ReadAsync(page, cancellationToken).ConfigureAwait(false);

            foreach (StoredEvent stored in events)
            {
                if (stored.Position > request.UpToPosition)
                {
                    LogReplayed(published, request.AfterPosition, after);

                    return published;
                }

                await transport.PublishAsync(stored.Message, cancellationToken).ConfigureAwait(false);
                published++;
                after = stored.Position;
            }

            if (events.Count < PageSize)
            {
                LogReplayed(published, request.AfterPosition, after);

                return published;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Replayed {Count} events after position {From} up to position {To}")]
    private partial void LogReplayed(int count, long from, long to);
}
