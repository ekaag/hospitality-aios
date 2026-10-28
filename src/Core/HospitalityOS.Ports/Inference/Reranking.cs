using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A reranker's ordering.
/// </summary>
/// <param name="Documents">Documents by descending score, each index at most once, at most TopN of them.</param>
/// <param name="Usage">Tokens used.</param>
/// <param name="Model">The model that answered.</param>
public sealed record Reranking(ValueList<RankedDocument> Documents, InferenceUsage Usage, string Model);
