namespace HospitalityOS.Ports.Inference;

/// <summary>
/// One provider call made for a task, for tracing (P11).
/// </summary>
/// <param name="Task">The task.</param>
/// <param name="Provider">The configured provider name, e.g. <c>anthropic</c>.</param>
/// <param name="Model">The model asked for.</param>
/// <param name="Prompt">The prompt version the call was given, or <c>null</c> when the task declares no prompt.</param>
/// <param name="Duration">How long the call took.</param>
/// <param name="Failure">Why it failed, or <c>null</c> when it succeeded.</param>
public sealed record InferenceAttempt(string Task, string Provider, string Model, PromptVersion? Prompt, TimeSpan Duration, string? Failure = null)
{
    /// <summary>
    /// Whether the call succeeded.
    /// </summary>
    public bool Succeeded => Failure is null;
}
