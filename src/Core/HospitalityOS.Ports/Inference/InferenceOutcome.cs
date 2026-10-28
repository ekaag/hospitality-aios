using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A task's result and how it was obtained.
/// </summary>
/// <param name="Value">What the successful call returned.</param>
/// <param name="Attempts">Every call made, in order; the last one succeeded and served the result.</param>
public sealed record InferenceOutcome<T>(T Value, ValueList<InferenceAttempt> Attempts)
{
    /// <summary>
    /// The call that served the result.
    /// </summary>
    public InferenceAttempt ServedBy => Attempts[^1];
}
