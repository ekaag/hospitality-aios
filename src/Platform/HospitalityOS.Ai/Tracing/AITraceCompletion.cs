using HospitalityOS.Ai.Grounding;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Ai.Tracing;

/// <summary>
/// How an AI turn ended, as the agent that ran it decided: what triggered it and what it answered.
/// </summary>
/// <param name="Trigger">What started the turn.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Response">The text sent, if any.</param>
/// <param name="Citations">The sources the response relies on; each must have been retrieved during the turn.</param>
/// <param name="PropertyId">The property the turn concerned, if any.</param>
/// <param name="Intent">What the agent understood was being asked for, e.g. <c>question</c>, if it decided; at most 100
/// characters.</param>
public sealed record AITraceCompletion(
    AITraceTrigger Trigger,
    AITraceOutcome Outcome,
    string? Response,
    ValueList<SourceReference> Citations,
    PropertyId? PropertyId = null,
    string? Intent = null)
{
    /// <summary>
    /// The completion for a grounded answer that was sent as given.
    /// </summary>
    /// <param name="trigger">What started the turn.</param>
    /// <param name="propertyId">The property asked about.</param>
    /// <param name="answer">The answer.</param>
    /// <param name="intent">The interpreted intent, if decided.</param>
    public static AITraceCompletion For(AITraceTrigger trigger, PropertyId propertyId, GroundedAnswer answer, string? intent = null)
    {
        ArgumentNullException.ThrowIfNull(answer);

        AITraceOutcome outcome = answer.Outcome is GroundedOutcome.Answered ? AITraceOutcome.Answered : AITraceOutcome.NoAuthoritativeSource;

        return new AITraceCompletion(trigger, outcome, answer.Text, answer.Citations, propertyId, intent);
    }
}
