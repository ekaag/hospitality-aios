using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Ai;

/// <summary>
/// The full account of an AI turn (spec §26); each list is in the order things happened.
/// </summary>
/// <param name="TriggerText">The triggering message's text, if it had any.</param>
/// <param name="Retrieved">Every authoritative source the agent had, read as context or returned by its tools, each at the
/// version read (spec §27).</param>
/// <param name="CapabilityCalls">Every capability call.</param>
/// <param name="ModelCalls">Every model call.</param>
/// <param name="Grounding">How the agent's replies were checked against the sources, for grounded answers.</param>
/// <param name="Response">The final response.</param>
/// <param name="Usage">Tokens used across every model call.</param>
public sealed record AITraceBody(
    string? TriggerText,
    ValueList<SourceReference> Retrieved,
    ValueList<CapabilityCall> CapabilityCalls,
    ValueList<ModelCall> ModelCalls,
    GroundingCheck? Grounding,
    TraceResponse Response,
    TokenUsage Usage);

/// <summary>
/// How a grounded answer's replies were checked.
/// </summary>
/// <param name="Replies">Each reply's verdict, in order; a rejected reply may be followed by a corrected one.</param>
public sealed record GroundingCheck(ValueList<ReplyVerdict> Replies);

/// <summary>
/// Whether one model reply was accepted.
/// </summary>
/// <param name="Accepted">Whether it was complete, well formed, and cited only sources the agent had (or said it could not
/// answer).</param>
/// <param name="Rejection">Why it was not accepted.</param>
public sealed record ReplyVerdict(bool Accepted, string? Rejection);

/// <summary>
/// What the agent finally answered.
/// </summary>
/// <param name="Text">The text sent, if any.</param>
/// <param name="Citations">The sources the answer relies on; empty when nothing authoritative answered.</param>
public sealed record TraceResponse(string? Text, ValueList<SourceReference> Citations);
