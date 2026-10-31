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
/// <param name="ExtraContext">Further reads the caller adds to the context after the property profile and content, with
/// inputs the caller vouches for, e.g. the guest's own stay (never one the model chose).</param>
public sealed record GroundedQuestion(
    string Task,
    PropertyId PropertyId,
    ValueList<ChatMessage> Messages,
    string NoSourceAnswer,
    IReadOnlyDictionary<string, string>? PromptValues = null,
    IReadOnlyList<ContextReadRequest>? ExtraContext = null);

/// <summary>
/// A capability read added to a grounded answer's context.
/// </summary>
/// <param name="Name">Its key in the context, e.g. <c>requests</c>; not <c>property</c> or <c>content</c>.</param>
/// <param name="Capability">The capability, read at version 1, e.g. <c>GuestService.GetRequestStatus</c>.</param>
/// <param name="Input">Its input, written in the wire format.</param>
public sealed record ContextReadRequest(string Name, string Capability, object Input);
