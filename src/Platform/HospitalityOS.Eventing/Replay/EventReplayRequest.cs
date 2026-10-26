using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Eventing.Replay;

/// <summary>
/// Which recorded events to publish again. Filters combine with AND.
/// </summary>
/// <param name="AfterPosition">Start after this log position; 0 starts from the beginning.</param>
/// <param name="UpToPosition">Stop after this log position, if set.</param>
/// <param name="TenantId">Only this tenant's events, if set.</param>
/// <param name="CorrelationId">Only one flow's events, if set.</param>
/// <param name="RecordedFrom">Only events recorded at or after this time, if set.</param>
/// <param name="RecordedBefore">Only events recorded before this time, if set.</param>
public sealed record EventReplayRequest(
    long AfterPosition = 0,
    long? UpToPosition = null,
    TenantId? TenantId = null,
    Guid? CorrelationId = null,
    DateTimeOffset? RecordedFrom = null,
    DateTimeOffset? RecordedBefore = null);
