using System.Text.Json;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// The assistant asking to call a tool. The platform validates and runs it (as a capability) and answers with a
/// <see cref="ChatToolResult"/> carrying the same <paramref name="CallId"/>.
/// </summary>
/// <param name="CallId">Identifies the call within the conversation (provider-assigned).</param>
/// <param name="Name">The tool, one of <see cref="ChatRequest.Tools"/>.</param>
/// <param name="Arguments">The arguments, a JSON object meant to match the tool's input schema (not yet validated).</param>
public sealed record ChatToolCall(string CallId, string Name, JsonElement Arguments) : ChatContent;
