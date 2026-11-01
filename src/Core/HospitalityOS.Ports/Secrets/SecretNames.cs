using System.Text.RegularExpressions;

namespace HospitalityOS.Ports.Secrets;

/// <summary>
/// What every <see cref="ISecretStore"/> accepts as a name, so a name works in every vault and as an environment
/// variable: lowercase words of letters and digits joined by single hyphens, at most <see cref="MaxLength"/> characters
/// (Key Vault allows 127).
/// </summary>
public static partial class SecretNames
{
    /// <summary>
    /// The longest name.
    /// </summary>
    public const int MaxLength = 127;

    /// <summary>
    /// Whether <paramref name="name"/> is a valid secret name.
    /// </summary>
    public static bool IsValid(string? name) => name is { Length: > 0 and <= MaxLength } && Pattern().IsMatch(name);

    /// <summary>
    /// Throws unless <paramref name="name"/> is a valid secret name.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not valid.</exception>
    public static void EnsureValid(string? name)
    {
        if (!IsValid(name))
        {
            throw new ArgumentException($"'{name}' is not a secret name: lowercase letters and digits in words joined by '-', at most {MaxLength} characters.", nameof(name));
        }
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
