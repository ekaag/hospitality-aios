namespace HospitalityOS.Domain.Capabilities.AITraces;

/// <summary>
/// Persistence port for <see cref="AITrace"/>. Implementations are scoped to the current tenant (P10) and commit through
/// the unit of work. Traces are never changed or deleted.
/// </summary>
public interface IAITraceRepository
{
    /// <summary>
    /// Loads a trace, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<AITrace?> GetByIdAsync(AITraceId id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the current tenant's traces matching <paramref name="query"/>, newest first (by completion time, then ID).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The limit is not between 1 and <see cref="AITraceQuery.MaxLimit"/>.</exception>
    Task<IReadOnlyList<AITrace>> ListAsync(AITraceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new trace, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(AITrace trace, CancellationToken cancellationToken);
}
