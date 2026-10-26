using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Ports.Eventing;

namespace HospitalityOS.Adapters.InMemory.Eventing;

internal sealed class InMemoryInboxStore(InMemoryStore store, InMemoryUnitOfWork unitOfWork) : IInboxStore
{
    public Task<bool> HasProcessedAsync(string handler, Guid eventId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handler);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(store.HasProcessed(new InboxKey(handler, eventId)));
    }

    public void Record(string handler, Guid eventId, DateTimeOffset processedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handler);

        unitOfWork.StageInboxRecord(new InboxKey(handler, eventId));
    }
}
