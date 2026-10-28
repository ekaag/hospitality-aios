using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Ai;

/// <summary>
/// One model call in an AI turn, with the exact prompt and messages sent and what came back. Messages the model already saw
/// are not repeated: when <paramref name="ContinuesConversation"/> is <c>true</c>, the conversation sent is the previous
/// call's conversation and output followed by this call's <paramref name="Input"/>.
/// </summary>
/// <param name="Kind">Chat or extraction.</param>
/// <param name="Task">The inference task, e.g. <c>concierge.answer</c>.</param>
/// <param name="Attempts">Every provider tried, in order; the last one answered unless the call failed.</param>
/// <param name="SystemPrompt">The rendered instructions sent, or <c>null</c> for none.</param>
/// <param name="ContinuesConversation">Whether this call continued the previous call's conversation.</param>
/// <param name="Input">The messages sent that the previous call's conversation and output did not already include.</param>
/// <param name="Tools">The tools offered, by name.</param>
/// <param name="StructuredOutput">Whether the answer had to match a JSON Schema.</param>
/// <param name="Output">What the model answered, or <c>null</c> when no provider could serve the call.</param>
/// <param name="StopReason">Why a chat model stopped, if it answered.</param>
/// <param name="Usage">Tokens used, if it answered.</param>
/// <param name="Model">The model that answered, as its provider names it.</param>
public sealed record ModelCall(
    ModelCallKind Kind,
    string Task,
    ValueList<ModelAttempt> Attempts,
    string? SystemPrompt,
    bool ContinuesConversation,
    ValueList<TraceMessage> Input,
    ValueList<string> Tools,
    bool StructuredOutput,
    TraceMessage? Output,
    TraceStopReason? StopReason,
    TokenUsage? Usage,
    string? Model);

/// <summary>
/// One provider tried for a model call.
/// </summary>
/// <param name="Provider">The configured provider, e.g. <c>anthropic</c>.</param>
/// <param name="Model">The model asked for.</param>
/// <param name="Prompt">The prompt version sent, or <c>null</c> when the task has none.</param>
/// <param name="DurationMilliseconds">How long the attempt took.</param>
/// <param name="Failure">Why it failed, or <c>null</c> when it answered.</param>
public sealed record ModelAttempt(string Provider, string Model, PromptVersionData? Prompt, double DurationMilliseconds, string? Failure);

/// <summary>
/// The exact prompt text a call used.
/// </summary>
/// <param name="Name">The prompt, e.g. <c>concierge.answer</c>.</param>
/// <param name="Number">Its version.</param>
/// <param name="Hash">The text's SHA-256 (<c>sha256:</c> and 64 hex digits).</param>
public sealed record PromptVersionData(string Name, int Number, string Hash);

/// <summary>
/// Tokens used.
/// </summary>
/// <param name="InputTokens">Tokens read.</param>
/// <param name="OutputTokens">Tokens generated.</param>
public sealed record TokenUsage(int InputTokens, int OutputTokens);
