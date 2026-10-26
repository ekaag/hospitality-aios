namespace HospitalityOS.Ports.Eventing;

/// <summary>
/// Which events each handler has already processed, so redelivered events cause their effect once (P8, Journey 19).
/// Scoped.
/// </summary>
/// <remarks>
/// A handler checks <see cref="HasProcessedAsync"/>, does its work, calls <see cref="Record"/>, and commits the
/// scope's <c>IUnitOfWork</c>: the record is saved in the same transaction as the handler's changes. If two
/// deliveries race past the check, the second commit fails with a duplicate-key conflict and none of its changes
/// are saved.
/// </remarks>
public interface IInboxStore
{
    /// <summary>
    /// Whether <paramref name="handler"/> has committed its processing of the event.
    /// </summary>
    /// <param name="handler">The handler's stable name, e.g. <c>GuestService.CompleteRequestOnHumanActionCompleted</c>.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    Task<bool> HasProcessedAsync(string handler, Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Stages the record that <paramref name="handler"/> processed the event; saved when the scope's <c>IUnitOfWork</c> commits.
    /// </summary>
    /// <param name="handler">The handler's stable name.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="processedAt">When (UTC).</param>
    void Record(string handler, Guid eventId, DateTimeOffset processedAt);
}
