namespace HospitalityOS.Domain.Capabilities.AITraces;

/// <summary>
/// A position in the newest-first list of traces: the last trace of the previous page.
/// </summary>
/// <param name="CompletedAt">When that trace completed.</param>
/// <param name="Id">That trace, which breaks ties between traces completed at the same time.</param>
public readonly record struct AITraceCursor(DateTimeOffset CompletedAt, AITraceId Id);
