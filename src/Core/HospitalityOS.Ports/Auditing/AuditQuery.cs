namespace HospitalityOS.Ports.Auditing;

/// <summary>
/// Which audit records to read, within the current tenant. Filters combine with AND.
/// </summary>
/// <param name="AfterPosition">Read records after this position; 0 reads from the start.</param>
/// <param name="Limit">The most records to return, from 1 to <see cref="MaxLimit"/>.</param>
/// <param name="CorrelationId">Only one flow's records, if set.</param>
/// <param name="TargetType">Only records about this kind of target, if set.</param>
/// <param name="TargetId">Only records about this target, if set.</param>
/// <param name="OccurredFrom">Only records at or after this time, if set.</param>
/// <param name="OccurredBefore">Only records before this time, if set.</param>
public sealed record AuditQuery(
    long AfterPosition = 0,
    int Limit = 100,
    Guid? CorrelationId = null,
    string? TargetType = null,
    string? TargetId = null,
    DateTimeOffset? OccurredFrom = null,
    DateTimeOffset? OccurredBefore = null)
{
    /// <summary>
    /// The largest page a single read returns.
    /// </summary>
    public const int MaxLimit = 1000;
}
