namespace HospitalityOS.Ports.Inference;

/// <summary>
/// The model's reasoning as its provider returned it, opaque to the platform. Keep it in the conversation exactly as
/// received: a provider may require its own reasoning back when the conversation continues (e.g. after tool calls);
/// adapters of other providers ignore it.
/// </summary>
/// <param name="Provider">The adapter that produced it, e.g. <c>Anthropic</c>.</param>
/// <param name="Data">The provider's state, in the adapter's own encoding.</param>
public sealed record ChatReasoning(string Provider, string Data) : ChatContent;
