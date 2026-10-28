namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Multi-turn chat with tool calls (the concierge and guest service conversations). Reached through
/// <see cref="IInferenceRouter"/> with a task name, never directly.
/// </summary>
public interface IChatModel
{
    /// <summary>
    /// Generates the assistant's next message: text, tool calls, or both.
    /// </summary>
    /// <param name="request">System prompt, conversation so far, and the tools the model may call.</param>
    /// <param name="model">The model and parameters the router chose.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The assistant's message, why it stopped, and usage.</returns>
    /// <exception cref="InferenceUnavailableException">The provider cannot serve the call now.</exception>
    Task<ChatCompletion> CompleteAsync(ChatRequest request, ModelSelection model, CancellationToken cancellationToken);
}
