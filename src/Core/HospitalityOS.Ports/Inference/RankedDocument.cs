namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A document's place in a <see cref="Reranking"/>.
/// </summary>
/// <param name="Index">Its position in <see cref="RerankRequest.Documents"/>.</param>
/// <param name="Score">Relevance; higher is more relevant (the scale is the provider's).</param>
public sealed record RankedDocument(int Index, double Score);
