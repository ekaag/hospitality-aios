using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A chat call.
/// </summary>
/// <param name="SystemPrompt">Instructions for the model (from the prompt registry), or <c>null</c>.</param>
/// <param name="Messages">The conversation so far, oldest first; must not be empty.</param>
/// <param name="Tools">Tools the model may call; only these (P7: allow-listed capabilities).</param>
public sealed record ChatRequest(string? SystemPrompt, ValueList<ChatMessage> Messages, ValueList<ChatTool>? Tools = null);
