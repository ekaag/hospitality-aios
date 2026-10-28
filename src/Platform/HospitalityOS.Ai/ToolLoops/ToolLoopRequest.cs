using System.Text.Json;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ai.ToolLoops;

/// <summary>
/// One turn for a tool loop.
/// </summary>
/// <param name="Task">The chat task declared with <c>AddToolLoop</c>, e.g. <c>concierge.answer</c>.</param>
/// <param name="Messages">The conversation so far, oldest first, ending with the guest's message.</param>
/// <param name="PromptValues">Values for the task prompt's variables; none when the task has no prompt.</param>
/// <param name="ResponseSchema">JSON Schema the model's final answer must match, or <c>null</c> for free text; the caller
/// validates the answer.</param>
public sealed record ToolLoopRequest(
    string Task,
    ValueList<ChatMessage> Messages,
    IReadOnlyDictionary<string, string>? PromptValues = null,
    JsonElement? ResponseSchema = null);
