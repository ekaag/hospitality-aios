using HospitalityOS.Ports.Eventing;

namespace HospitalityOS.UnitTests.Eventing.TestDoubles;

/// <summary>
/// An outbox that hands out a fixed set of due entries and records what the dispatcher did with them.
/// </summary>
internal sealed class RecordingOutboxStore(params OutboxEntry[] due) : IOutboxStore
{
    public List<(DateTimeOffset Now, int MaxCount, TimeSpan Lease)> Claims { get; } = [];

    public List<Guid> Dispatched { get; } = [];

    public List<(Guid EventId, DateTimeOffset RetryAt, string Error)> Retries { get; } = [];

    public List<(Guid EventId, DateTimeOffset At, string Error)> DeadLetters { get; } = [];

    public Task<IReadOnlyList<OutboxEntry>> ClaimDueAsync(DateTimeOffset now, int maxCount, TimeSpan lease, CancellationToken cancellationToken)
    {
        Claims.Add((now, maxCount, lease));

        return Task.FromResult<IReadOnlyList<OutboxEntry>>(due);
    }

    public Task MarkDispatchedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        Dispatched.Add(eventId);

        return Task.CompletedTask;
    }

    public Task ScheduleRetryAsync(Guid eventId, DateTimeOffset retryAt, string error, CancellationToken cancellationToken)
    {
        Retries.Add((eventId, retryAt, error));

        return Task.CompletedTask;
    }

    public Task DeadLetterAsync(Guid eventId, DateTimeOffset at, string error, CancellationToken cancellationToken)
    {
        DeadLetters.Add((eventId, at, error));

        return Task.CompletedTask;
    }
}
