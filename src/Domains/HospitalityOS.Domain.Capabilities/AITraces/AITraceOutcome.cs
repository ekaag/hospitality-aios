namespace HospitalityOS.Domain.Capabilities.AITraces;

/// <summary>
/// How an AI turn ended, as the agent that ran it decided.
/// </summary>
public enum AITraceOutcome
{
    /// <summary>
    /// The agent answered from authoritative sources.
    /// </summary>
    Answered,

    /// <summary>
    /// No authoritative source answered the question, so the agent said so instead of answering (spec §28).
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
}
