namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Identifies the exact prompt text an inference call used (P6, P11). Released versions never change: an edit is a new
/// version, so a name and number always mean the same text, and <paramref name="Hash"/> proves it.
/// </summary>
/// <param name="Name">The prompt's name, e.g. <c>concierge.answer</c>; usually the task's name.</param>
/// <param name="Number">The version, numbered from 1.</param>
/// <param name="Hash">The text's SHA-256 (<c>sha256:</c> and 64 lowercase hex digits), taken with LF line endings.</param>
public sealed record PromptVersion(string Name, int Number, string Hash)
{
    /// <summary>
    /// The name and number, e.g. <c>concierge.answer@2</c>.
    /// </summary>
    public override string ToString() => $"{Name}@{Number}";
}
