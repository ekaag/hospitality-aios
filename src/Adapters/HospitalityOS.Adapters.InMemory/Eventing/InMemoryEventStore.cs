using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Ports.Eventing;

namespace HospitalityOS.Adapters.InMemory.Eventing;

internal sealed class InMemoryEventStore(InMemoryStore store, InMemoryUnitOfWork unitOfWork) : IEventStore
{
    public void Append(EventMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        unitOfWork.StageEvent(message);
    }

    public Task<IReadOnlyList<StoredEvent>> ReadAsync(EventStoreQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.AfterPosition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.Limit, EventStoreQuery.MaxLimit);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(store.ReadEvents(query));
    }
}
