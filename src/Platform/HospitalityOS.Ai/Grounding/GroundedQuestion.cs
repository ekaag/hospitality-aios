using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Ai.Grounding;

/// <summary>
/// A guest question to answer from a property's authoritative information.
/// </summary>
/// <param name="Task">The chat task declared with <c>AddGroundedAnswering</c>, e.g. <c>concierge.answer</c>.</param>
/// <param name="PropertyId">The property the guest is asking about.</param>
/// <param name="Messages">The conversation so far, oldest first, ending with the guest's question.</param>
/// <param name="NoSourceAnswer">What the guest is told when no authoritative source answers the question, e.g. "I don't
/// have confirmed information about that; please ask the front desk."</param>
/// <param name="PromptValues">Values for the prompt's other variables; <c>context</c> is filled in by the answerer.</param>
public sealed record GroundedQuestion(
    string Task,
    PropertyId PropertyId,
    ValueList<ChatMessage> Messages,
    string NoSourceAnswer,
    IReadOnlyDictionary<string, string>? PromptValues = null);
