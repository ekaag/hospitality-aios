using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ai.Tracing;

/// <summary>
/// Converts inference messages to the trace's wire format.
/// </summary>
internal static class TraceMapping
{
    public static TraceMessage Message(ChatMessage message) =>
        new(Role(message.Role), ValueList.From(message.Content.Select(Part)));

    public static TraceStopReason StopReason(ChatStopReason reason) => reason switch
    {
        ChatStopReason.Completed => TraceStopReason.Completed,
        ChatStopReason.ToolCalls => TraceStopReason.ToolCalls,
        ChatStopReason.MaxTokens => TraceStopReason.MaxTokens,
        ChatStopReason.ContentFiltered => TraceStopReason.ContentFiltered,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown stop reason."),
    };

    private static TraceMessageRole Role(ChatRole role) => role switch
    {
        ChatRole.User => TraceMessageRole.User,
        ChatRole.Assistant => TraceMessageRole.Assistant,
        ChatRole.Tool => TraceMessageRole.Tool,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown chat role."),
    };

    private static TracePart Part(ChatContent content) => content switch
    {
        ChatText text => new TracePart(TracePartType.Text, Text: text.Text),
        ChatToolCall call => new TracePart(TracePartType.ToolCall, CallId: call.CallId, Name: call.Name, Arguments: call.Arguments.Clone()),
        ChatToolResult result => new TracePart(TracePartType.ToolResult, Text: result.Content, CallId: result.CallId, IsError: result.IsError),

        // Reasoning is opaque provider state (often encrypted), so only that it happened is kept.
        ChatReasoning reasoning => new TracePart(TracePartType.Reasoning, Provider: reasoning.Provider),
        _ => throw new ArgumentOutOfRangeException(nameof(content), content.GetType().Name, "Unknown chat content."),
    };
}
