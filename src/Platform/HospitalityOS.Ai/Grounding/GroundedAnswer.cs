using HospitalityOS.Ai.ToolLoops;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ai.Grounding;

/// <summary>
/// Whether a question was answered from an authoritative source.
/// </summary>
public enum GroundedOutcome
{
    /// <summary>
    /// Answered, citing the sources it relies on.
    /// </summary>
    Answered,

    /// <summary>
    /// No authoritative source answers it (Journey 17): the guest gets the caller's honest answer, and the question is
    /// worth recording for content improvement.
    /// </summary>
    NoAuthoritativeSource,

    /// <summary>
    /// The sources are relevant but do not clearly settle the question, e.g. an ambiguous policy (Journey 6): the guest is
    /// told what they say and that it is not confirmed, citing them. Also worth recording for content improvement.
    /// </summary>
    Uncertain,
}

/// <summary>
/// A grounded answer and what it rests on, for the caller and for tracing (P11, spec §26).
/// </summary>
/// <param name="Outcome">Whether an authoritative source answered the question.</param>
/// <param name="Text">What to tell the guest: the model's answer, or the caller's no-source answer.</param>
/// <param name="Citations">The sources the answer relies on; empty when no authoritative source answered.</param>
/// <param name="Retrieved">Every source the context and tool calls returned in this turn: the answer may cite only these.</param>
/// <param name="Rejection">Why the model's last reply was not accepted (not grounded, malformed, or cut off), or
/// <c>null</c>.</param>
/// <param name="Context">The reads that built the context, in order.</param>
/// <param name="Added">The messages the turn added, in order, including any correction sent to the model.</param>
/// <param name="ToolCalls">Every tool call the model made.</param>
/// <param name="Attempts">Every provider call.</param>
/// <param name="Usage">Tokens used.</param>
public sealed record GroundedAnswer(
    GroundedOutcome Outcome,
    string Text,
    ValueList<SourceReference> Citations,
    ValueList<SourceReference> Retrieved,
    string? Rejection,
    ValueList<ContextRead> Context,
    ValueList<ChatMessage> Added,
    ValueList<ToolCallRecord> ToolCalls,
    ValueList<InferenceAttempt> Attempts,
    InferenceUsage Usage);

/// <summary>
/// A capability read that built the context.
/// </summary>
/// <param name="Capability">What ran, e.g. <c>Property.GetProperty@1</c>.</param>
/// <param name="InvocationId">Its invocation, when it succeeded.</param>
/// <param name="ErrorCode">Why it failed, or <c>null</c>.</param>
public sealed record ContextRead(string Capability, Guid? InvocationId, string? ErrorCode);
