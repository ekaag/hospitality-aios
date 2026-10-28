namespace HospitalityOS.Ports.Inference;

/// <summary>
/// What a call consumed, as the provider reported it (P6: every call records usage).
/// </summary>
/// <param name="InputTokens">Tokens read.</param>
/// <param name="OutputTokens">Tokens generated.</param>
public sealed record InferenceUsage(int InputTokens, int OutputTokens)
{
    /// <summary>
    /// Nothing consumed, e.g. a fake model.
    /// </summary>
    public static InferenceUsage None { get; } = new(0, 0);
}
