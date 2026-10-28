namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A judge's verdict.
/// </summary>
/// <param name="Score">How well the output meets the criteria, 0 to 1.</param>
/// <param name="Passed">Whether it meets them.</param>
/// <param name="Reasoning">Why, for people reviewing an evaluation run.</param>
/// <param name="Usage">Tokens used.</param>
/// <param name="Model">The model that answered.</param>
public sealed record Judgement(double Score, bool Passed, string Reasoning, InferenceUsage Usage, string Model);
