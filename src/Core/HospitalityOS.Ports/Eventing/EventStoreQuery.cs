using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Ports.Eventing;

/// <summary>
/// Which recorded events to read (replay by range or correlation, C6). Filters combine with AND.
/// </summary>
/// <param name="AfterPosition">Read events after this log position; 0 reads from the start.</param>
/// <param name="Limit">The most events to return, from 1 to <see cref="MaxLimit"/>.</param>
/// <param name="TenantId">Only this tenant's events, if set.</param>
/// <param name="CorrelationId">Only one flow's events, if set.</param>
/// <param name="RecordedFrom">Only events recorded at or after this time, if set.</param>
/// <param name="RecordedBefore">Only events recorded before this time, if set.</param>
public sealed record EventStoreQuery(
    long AfterPosition = 0,
    int Limit = 100,
    TenantId? TenantId = null,
    Guid? CorrelationId = null,
    DateTimeOffset? RecordedFrom = null,
    DateTimeOffset? RecordedBefore = null)
{
    /// <summary>
    /// The largest page a single read returns.
    /// </summary>
    public const int MaxLimit = 1000;
}
