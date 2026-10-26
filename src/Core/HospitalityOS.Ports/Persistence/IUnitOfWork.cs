using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ports.Persistence;

/// <summary>
/// Saves, as one atomic step, every aggregate added or changed through the repositories of the current scope
/// (one request or one message), together with the domain events they raised (P1) and the writes staged through
/// <c>IEventStore.Append</c> and <c>IInboxStore.Record</c>. Scoped: one per request.
/// </summary>
/// <remarks>
/// Repositories track every aggregate they return, so callers change aggregates through their methods and then
/// commit; there is no explicit update call. Within a scope, loading the same aggregate twice returns the same
/// instance, and repository queries (<c>Find…</c>, <c>List…</c>) match committed data only, so an aggregate
/// added but not yet committed is not found by them. Each changed aggregate must still be at the version it was loaded at
/// (optimistic concurrency), and the commit must not break a unique key (for example two units numbered 412 in
/// one property). Either failure is an expected conflict, not an exception, so a retried request can reload and
/// try again (P8).
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves every added and changed aggregate and the events they raised, all or nothing. On success, the events
    /// are removed from the aggregates (so a later commit does not save them twice) and returned in raise order.
    /// On failure nothing is saved and the aggregates keep their events.
    /// </summary>
    /// <param name="cancellationToken">Cancels the commit before anything is saved.</param>
    /// <returns>
    /// The committed events, or a <see cref="ErrorKind.Conflict"/> error: <see cref="PersistenceErrors.ConcurrencyConflictCode"/>
    /// when another scope saved one of the aggregates first, <see cref="PersistenceErrors.DuplicateKeyCode"/> when an ID or
    /// unique key is already taken.
    /// </returns>
    Task<Result<CommitReceipt>> CommitAsync(CancellationToken cancellationToken);
}
