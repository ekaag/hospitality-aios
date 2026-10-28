using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ai.ToolLoops;

/// <summary>
/// How a tool loop turn ended and what led there, for the caller and for tracing (P11).
/// </summary>
/// <param name="Answer">The model's last message.</param>
/// <param name="StopReason">Why it stopped: <see cref="ChatStopReason.Completed"/>, or
/// <see cref="ChatStopReason.MaxTokens"/> or <see cref="ChatStopReason.ContentFiltered"/>, which the caller decides how
/// to handle.</param>
/// <param name="Added">The messages the turn added, in order: each model message as received (keep them unchanged in
/// the conversation) and each round's tool results.</param>
/// <param name="ToolCalls">Every tool call the model made, in order, with how it went.</param>
/// <param name="Attempts">Every provider call, in order, across all rounds.</param>
/// <param name="Usage">Tokens used across all rounds.</param>
public sealed record ToolLoopResult(
    ChatMessage Answer,
    ChatStopReason StopReason,
    ValueList<ChatMessage> Added,
    ValueList<ToolCallRecord> ToolCalls,
    ValueList<InferenceAttempt> Attempts,
    InferenceUsage Usage);
