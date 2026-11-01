using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Eventing;

internal sealed class InMemoryOutboxStore(InMemoryStore store) : IOutboxStore
{
    public Task<IReadOnlyList<OutboxEntry>> ClaimDueAsync(DateTimeOffset now, int maxCount, TimeSpan lease, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lease, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(store.ClaimDue(now, maxCount, lease));
    }

    public Task MarkDispatchedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        store.MarkDispatched(eventId);

        return Task.CompletedTask;
    }

    public Task ScheduleRetryAsync(Guid eventId, DateTimeOffset retryAt, string error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);
        cancellationToken.ThrowIfCancellationRequested();
        store.ScheduleRetry(eventId, retryAt, error);

        return Task.CompletedTask;
    }

    public Task DeadLetterAsync(Guid eventId, DateTimeOffset at, string error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);
        cancellationToken.ThrowIfCancellationRequested();
        store.DeadLetter(eventId, at, error);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeadLetteredOutboxEvent>> ListDeadLettersAsync(TenantId tenantId, int maxCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(store.DeadLetters(tenantId, maxCount));
    }

    public Task<bool> RequeueAsync(TenantId tenantId, Guid eventId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(store.Requeue(tenantId, eventId));
    }
}
