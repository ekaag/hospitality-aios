using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai.Tracing;

/// <summary>
/// Reads the current tenant's AI traces for staff (spec §26), redacted for the reader's role
/// (<see cref="AITraceRedactionOptions"/>). Scoped; the reader's tenant and role come from the operation.
/// </summary>
public interface IAITraceReader
{
    /// <summary>
    /// Reads one trace, with the events recorded in its flow.
    /// </summary>
    /// <returns>The trace, or <c>ai_trace.not_found</c>.</returns>
    Task<Result<AITraceResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists traces, newest first.
    /// </summary>
    /// <returns>A page, or a <see cref="ValidationError"/> for an invalid <c>limit</c> or <c>after</c>.</returns>
    Task<Result<AITracePage>> ListAsync(AITraceListRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Which traces to list. Filters combine with AND.
/// </summary>
/// <param name="PropertyId">Only one property's traces, if set.</param>
/// <param name="CorrelationId">Only one flow's traces, if set.</param>
/// <param name="Outcome">Only traces that ended this way, if set.</param>
/// <param name="CompletedFrom">Only traces completed at or after this time, if set.</param>
/// <param name="CompletedBefore">Only traces completed before this time, if set.</param>
/// <param name="After">The previous page's <see cref="AITracePage.Next"/>, to continue from it.</param>
/// <param name="Limit">The most traces to return, from 1 to 200; default 50.</param>
public sealed record AITraceListRequest(
    PropertyId? PropertyId = null,
    Guid? CorrelationId = null,
    AITraceOutcome? Outcome = null,
    DateTimeOffset? CompletedFrom = null,
    DateTimeOffset? CompletedBefore = null,
    string? After = null,
    int? Limit = null);
