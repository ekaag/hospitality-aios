namespace HospitalityOS.Contracts.Api.Ai;

/// <summary>
/// How an AI turn ended.
/// </summary>
public enum AITraceOutcome
{
    /// <summary>
    /// The agent answered from authoritative sources.
    /// </summary>
    Answered,

    /// <summary>
    /// No authoritative source answered the question, so the agent said so instead of answering.
    /// </summary>
    NoAuthoritativeSource,

    /// <summary>
    /// The turn could not be completed, e.g. no model was available.
    /// </summary>
    Failed,

    /// <summary>
    /// The agent answered from relevant sources that do not clearly settle the question, and said so.
    /// </summary>
    Uncertain,

    /// <summary>
    /// The agent decided who handles the request, e.g. triaging a guest message by its intent.
    /// </summary>
    Routed,
}
