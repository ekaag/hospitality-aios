using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Capabilities.AITraces;

/// <summary>
/// Which traces to list, within the current tenant, newest first. Filters combine with AND.
/// </summary>
/// <param name="Limit">The most traces to return, from 1 to <see cref="MaxLimit"/>.</param>
/// <param name="After">Continue after this trace (the last one of the previous page), if set.</param>
/// <param name="PropertyId">Only one property's traces, if set.</param>
/// <param name="CorrelationId">Only one flow's traces, if set.</param>
/// <param name="Outcome">Only traces that ended this way, if set.</param>
/// <param name="CompletedFrom">Only traces completed at or after this time, if set.</param>
/// <param name="CompletedBefore">Only traces completed before this time, if set.</param>
public sealed record AITraceQuery(
    int Limit = 50,
    AITraceCursor? After = null,
    PropertyId? PropertyId = null,
    Guid? CorrelationId = null,
    AITraceOutcome? Outcome = null,
    DateTimeOffset? CompletedFrom = null,
    DateTimeOffset? CompletedBefore = null)
{
    /// <summary>
    /// The largest page a single read returns.
    /// </summary>
    public const int MaxLimit = 200;
}
