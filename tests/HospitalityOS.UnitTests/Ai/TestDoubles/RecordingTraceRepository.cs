using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Ports.Eventing;

namespace HospitalityOS.UnitTests.Ai.TestDoubles;

/// <summary>
/// The traces staged through <see cref="RecordingTraceRepository"/>, across scopes.
/// </summary>
internal sealed class TraceStore
{
    public List<AITrace> Added { get; } = [];
}

/// <summary>
/// Stands in for trace persistence, which has its own contract tests: keeps what was added.
/// </summary>
internal sealed class RecordingTraceRepository(TraceStore store) : IAITraceRepository
{
    public Task<AITrace?> GetByIdAsync(AITraceId id, CancellationToken cancellationToken) =>
        Task.FromResult(store.Added.FirstOrDefault(trace => trace.Id == id));

    public Task<IReadOnlyList<AITrace>> ListAsync(AITraceQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Listing is covered by the repository contract tests.");

    public Task AddAsync(AITrace trace, CancellationToken cancellationToken)
    {
        store.Added.Add(trace);

        return Task.CompletedTask;
    }
}

/// <summary>
/// An event store holding <see cref="Events"/>, for reading a trace's flow; it answers queries like the real store.
/// </summary>
internal sealed class ListedEventStore : IEventStore
{
    public List<StoredEvent> Events { get; } = [];

    public List<EventStoreQuery> Queries { get; } = [];

    public void Append(EventMessage message) => throw new NotSupportedException("Read only.");

    public Task<IReadOnlyList<StoredEvent>> ReadAsync(EventStoreQuery query, CancellationToken cancellationToken)
    {
        Queries.Add(query);

        return Task.FromResult<IReadOnlyList<StoredEvent>>([.. Events
            .Where(stored => stored.Position > query.AfterPosition)
            .Where(stored => query.TenantId is not { } tenant || stored.Message.TenantId == tenant)
            .Where(stored => query.CorrelationId is not { } correlation || stored.Message.CorrelationId == correlation)
            .Take(query.Limit)]);
    }
}
