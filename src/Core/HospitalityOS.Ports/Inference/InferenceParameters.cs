namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Provider-neutral generation settings bound to a task; <c>null</c> leaves the provider's default. Sampling settings
/// (temperature, top-p) are deliberately absent: current models are steered by effort instead, and some providers
/// reject sampling settings outright.
/// </summary>
/// <param name="MaxOutputTokens">Longest output, in tokens (reasoning included where the provider counts it).</param>
/// <param name="Effort">How much reasoning to spend: low for routing and extraction, higher for answers that need care.</param>
public sealed record InferenceParameters(int? MaxOutputTokens = null, InferenceEffort? Effort = null)
{
    /// <summary>
    /// The provider's defaults for everything.
    /// </summary>
    public static InferenceParameters Default { get; } = new();
}
