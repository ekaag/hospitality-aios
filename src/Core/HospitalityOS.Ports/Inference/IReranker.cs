namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Orders retrieved passages by relevance to a query (optional step in G5).
/// </summary>
public interface IReranker
{
    /// <summary>
    /// Scores the documents against the query, most relevant first.
    /// </summary>
    /// <exception cref="InferenceUnavailableException">The provider cannot serve the call now.</exception>
    Task<Reranking> RerankAsync(RerankRequest request, ModelSelection model, CancellationToken cancellationToken);
}
