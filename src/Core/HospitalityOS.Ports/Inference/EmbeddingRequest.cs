using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// An embedding call.
/// </summary>
/// <param name="Inputs">Texts to embed; at least one.</param>
/// <param name="Dimensions">Vector length, for models that can shorten their vectors; <c>null</c> for the model's own.</param>
public sealed record EmbeddingRequest(ValueList<string> Inputs, int? Dimensions = null);
