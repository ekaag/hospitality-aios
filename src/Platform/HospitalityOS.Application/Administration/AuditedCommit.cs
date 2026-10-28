using HospitalityOS.Application.Auditing;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Administration;

/// <summary>
/// Ends an administrative command: records its outcome in the audit trail (spec Journey 1: actor, action, time, target)
/// and commits the record with the command's changes, so a change is never saved without its audit record or the
/// other way round. Scoped.
/// </summary>
internal sealed class AuditedCommit(IUnitOfWork unitOfWork, IAuditTrail auditTrail)
{
    /// <summary>
    /// Ends a create command: adds and commits the new aggregate with its audit record, or audits the rejection.
    /// </summary>
    /// <returns>The response; the rejection; or the commit's conflict (nothing saved).</returns>
    public async Task<Result<TResponse>> CreateAsync<TAggregate, TResponse>(
        string action,
        Result<TAggregate> created,
        Func<TAggregate, Task> add,
        Func<TAggregate, AuditTarget> target,
        Func<TAggregate, TResponse> respond,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(created);
        ArgumentNullException.ThrowIfNull(add);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(respond);

        if (created.IsFailure)
        {
            return await RejectAsync(action, created.Error, null, cancellationToken).ConfigureAwait(false);
        }

        await add(created.Value).ConfigureAwait(false);
        Result committed = await SucceedAsync(action, target(created.Value), cancellationToken).ConfigureAwait(false);

        return committed.IsFailure ? committed.Error : respond(created.Value);
    }

    /// <summary>
    /// Ends a command that changes an existing aggregate: refuses a missing one or one no longer at
    /// <paramref name="expectedVersion"/>, applies <paramref name="change"/>, and commits it with its audit record. Any
    /// refusal is audited and saves nothing else.
    /// </summary>
    /// <returns>The response; <paramref name="notFound"/>; <c>request.version_mismatch</c>; the change's error; or the
    /// commit's conflict.</returns>
    public async Task<Result<TResponse>> ChangeAsync<TAggregate, TResponse>(
        string action,
        AuditTarget target,
        TAggregate? aggregate,
        Error notFound,
        long? expectedVersion,
        Func<TAggregate, Task<Result>> change,
        Func<TAggregate, TResponse> respond,
        CancellationToken cancellationToken)
        where TAggregate : class, IAggregateRoot
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(respond);

        Result changed = aggregate is null ? notFound
            : expectedVersion is { } expected && expected != aggregate.Version ? AdministrationErrors.VersionMismatch(expected, aggregate.Version)
            : await change(aggregate).ConfigureAwait(false);

        if (changed.IsFailure)
        {
            return await RejectAsync(action, changed.Error, target, cancellationToken).ConfigureAwait(false);
        }

        Result committed = await SucceedAsync(action, target, cancellationToken).ConfigureAwait(false);

        return committed.IsFailure ? committed.Error : respond(aggregate!);
    }

    /// <summary>
    /// Records success and commits the command's staged changes with it.
    /// </summary>
    /// <returns>Success, or the commit's conflict (nothing saved).</returns>
    public async Task<Result> SucceedAsync(string action, AuditTarget target, CancellationToken cancellationToken)
    {
        auditTrail.Record(action, AuditOutcome.Succeeded, target);
        Result<CommitReceipt> commit = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return commit.IsFailure ? commit.Error : Result.Success();
    }

    /// <summary>
    /// Records a rejected command and commits only that record: a rejected command stages nothing.
    /// </summary>
    /// <returns><paramref name="error"/>, for the caller to return.</returns>
    public async Task<Error> RejectAsync(string action, Error error, AuditTarget? target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);

        auditTrail.Record(action, AuditOutcome.Failed, target, error.Code);
        Result<CommitReceipt> commit = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        // Only a new audit record is staged, so a failed commit is an infrastructure fault, never a conflict.
        return commit.IsSuccess ? error : throw new InvalidOperationException($"Could not record the rejected {action}: {commit.Error.Message}");
    }
}
