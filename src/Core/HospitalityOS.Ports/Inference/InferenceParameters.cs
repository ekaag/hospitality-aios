namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Provider-neutral generation settings bound to a task; <c>null</c> leaves the provider's default.
/// </summary>
/// <param name="Temperature">Randomness, 0 to 2; low for factual answers and extraction.</param>
/// <param name="MaxOutputTokens">Longest output, in tokens.</param>
/// <param name="TopP">Nucleus sampling, 0 to 1.</param>
public sealed record InferenceParameters(double? Temperature = null, int? MaxOutputTokens = null, double? TopP = null)
{
    /// <summary>
    /// The provider's defaults for everything.
    /// </summary>
    public static InferenceParameters Default { get; } = new();
}
