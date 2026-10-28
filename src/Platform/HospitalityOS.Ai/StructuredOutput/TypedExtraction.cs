using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ai.StructuredOutput;

/// <summary>
/// A validated extraction and how it was obtained.
/// </summary>
/// <param name="Value">The extracted value.</param>
/// <param name="Attempts">Every provider call, in order, including one whose output was invalid and sent back.</param>
/// <param name="Usage">Tokens used across all calls.</param>
public sealed record TypedExtraction<T>(T Value, ValueList<InferenceAttempt> Attempts, InferenceUsage Usage);
