namespace HospitalityOS.Ports.Inference;

/// <summary>
/// The platform's answer to a <see cref="ChatToolCall"/>.
/// </summary>
/// <param name="CallId">The call answered.</param>
/// <param name="Content">The result, usually the capability's JSON output.</param>
/// <param name="IsError">Whether the call failed (the content then explains why, for the model to recover).</param>
public sealed record ChatToolResult(string CallId, string Content, bool IsError = false) : ChatContent;
