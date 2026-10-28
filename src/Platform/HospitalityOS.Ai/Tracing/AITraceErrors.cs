namespace HospitalityOS.Ai.Tracing;

/// <summary>
/// Expected failures when reading AI traces.
/// </summary>
public static class AITraceErrors
{
    /// <summary>
    /// No trace with that ID exists in the reader's tenant.
    /// </summary>
    public const string NotFoundCode = "ai_trace.not_found";
}
