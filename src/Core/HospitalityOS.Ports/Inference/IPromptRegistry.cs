namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Versioned prompts, independent of providers (P6). Callers rarely use it directly: <see cref="IInferenceRouter"/> hands
/// each call its task's prompt at the bound version. Singleton.
/// </summary>
public interface IPromptRegistry
{
    /// <summary>
    /// The prompt <paramref name="name"/> at version <paramref name="number"/>, or its latest version when
    /// <paramref name="number"/> is <c>null</c>.
    /// </summary>
    /// <param name="name">The prompt's name, e.g. <c>concierge.answer</c>; case-insensitive.</param>
    /// <param name="number">The version, or <c>null</c> for the latest.</param>
    /// <returns>The prompt, or <c>null</c> when there is no such prompt or version.</returns>
    Prompt? Find(string name, int? number = null);
}
