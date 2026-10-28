namespace HospitalityOS.Ports.Inference;

/// <summary>
/// An embedding model's vectors.
/// </summary>
/// <param name="Vectors">One vector per input, in input order, all the same length.</param>
/// <param name="Usage">Tokens used.</param>
/// <param name="Model">The model that answered.</param>
public sealed record Embeddings(IReadOnlyList<ReadOnlyMemory<float>> Vectors, InferenceUsage Usage, string Model);
