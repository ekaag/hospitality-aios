namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A classifier's choice.
/// </summary>
/// <param name="Label">One of the request's labels, exactly as given.</param>
/// <param name="Confidence">How sure the model is, 0 to 1, when the provider reports it.</param>
/// <param name="Usage">Tokens used.</param>
/// <param name="Model">The model that answered.</param>
public sealed record Classification(string Label, double? Confidence, InferenceUsage Usage, string Model);
