namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Turns texts into vectors for content retrieval (G5).
/// </summary>
public interface IEmbeddingModel
{
    /// <summary>
    /// Embeds each input, in order.
    /// </summary>
    /// <exception cref="InferenceUnavailableException">The provider cannot serve the call now.</exception>
    Task<Embeddings> EmbedAsync(EmbeddingRequest request, ModelSelection model, CancellationToken cancellationToken);
}
