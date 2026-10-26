using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ports.Persistence;

/// <summary>
/// The expected failures of <see cref="IUnitOfWork.CommitAsync"/>, identical for every persistence adapter so callers
/// and contract tests never depend on the technology.
/// </summary>
public static class PersistenceErrors
{
    /// <summary>
    /// Code returned when another scope saved a changed aggregate after it was loaded.
    /// </summary>
    public const string ConcurrencyConflictCode = "persistence.concurrency_conflict";

    /// <summary>
    /// Code returned when an added aggregate's ID, or a unique key, is already taken.
    /// </summary>
    public const string DuplicateKeyCode = "persistence.duplicate_key";

    /// <summary>
    /// Another scope saved the aggregate first; reload it and retry.
    /// </summary>
    /// <param name="aggregateType">The aggregate's type name.</param>
    /// <param name="aggregateId">The aggregate's ID.</param>
    /// <returns>The <see cref="ErrorKind.Conflict"/> error.</returns>
    public static Error ConcurrencyConflict(string aggregateType, Guid aggregateId) =>
        Error.Conflict(ConcurrencyConflictCode, $"{aggregateType} {aggregateId} was changed by someone else; reload it and try again.");

    /// <summary>
    /// The aggregate's ID or one of its unique keys is already taken.
    /// </summary>
    /// <param name="aggregateType">The aggregate's type name.</param>
    /// <param name="key">Which key, e.g. <c>id</c> or <c>unitNumber</c>.</param>
    /// <returns>The <see cref="ErrorKind.Conflict"/> error.</returns>
    public static Error DuplicateKey(string aggregateType, string key) =>
        Error.Conflict(DuplicateKeyCode, $"Another {aggregateType} already has this {key}.");
}
