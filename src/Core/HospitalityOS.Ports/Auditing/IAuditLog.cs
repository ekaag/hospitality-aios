namespace HospitalityOS.Ports.Auditing;

/// <summary>
/// The append-only audit log (P10, spec §37). There is no way to change or delete a record through it; deployments
/// also deny those rights at the database (K1). Scoped to the current tenant: a scope reads and writes only its own
/// tenant's records.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Stages a record; it is saved when the scope's <c>IUnitOfWork</c> commits, together with the changes it describes,
    /// and not at all if that commit fails. To record a failed or denied attempt, commit a scope that holds no other changes.
    /// </summary>
    /// <param name="record">The record; must belong to the current tenant.</param>
    /// <exception cref="InvalidOperationException">The record belongs to another tenant.</exception>
    void Append(AuditRecord record);

    /// <summary>
    /// Reads the current tenant's saved records, oldest first.
    /// </summary>
    /// <param name="query">Which records, from where, and how many.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<IReadOnlyList<AuditEntry>> ReadAsync(AuditQuery query, CancellationToken cancellationToken);
}
