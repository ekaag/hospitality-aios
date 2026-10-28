namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A chat model's reply.
/// </summary>
/// <param name="Message">The assistant's message (role <see cref="ChatRole.Assistant"/>).</param>
/// <param name="StopReason">Why generation stopped; <see cref="ChatStopReason.ToolCalls"/> exactly when the message
/// holds tool calls.</param>
/// <param name="Usage">Tokens used.</param>
/// <param name="Model">The model that answered, as the provider names it.</param>
public sealed record ChatCompletion(ChatMessage Message, ChatStopReason StopReason, InferenceUsage Usage, string Model);
