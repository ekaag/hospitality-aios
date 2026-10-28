using HospitalityOS.Application.Auditing;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Persistence;
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
