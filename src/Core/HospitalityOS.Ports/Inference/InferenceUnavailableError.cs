using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// No provider bound to a task could serve it: every attempt threw <see cref="InferenceUnavailableException"/>. Callers
/// tell the guest honestly they cannot help right now (P4) rather than guess.
/// </summary>
public sealed record InferenceUnavailableError : Error
{
    /// <summary>
    /// The error's code.
    /// </summary>
    public const string UnavailableCode = "inference.unavailable";

    /// <summary>
    /// Creates the error.
    /// </summary>
    /// <param name="task">The task no provider could serve.</param>
    /// <param name="attempts">Every call made, all failed.</param>
    public InferenceUnavailableError(string task, ValueList<InferenceAttempt> attempts)
        : base(UnavailableCode, $"No provider could serve the inference task '{task}' ({attempts?.Count ?? 0} attempted).", ErrorKind.Unavailable)
    {
        Task = task;
        Attempts = attempts ?? [];
    }

    /// <summary>
    /// The task.
    /// </summary>
    public string Task { get; }

    /// <summary>
    /// Every call made, all failed, in order.
    /// </summary>
    public ValueList<InferenceAttempt> Attempts { get; }
}
