using System.Text.RegularExpressions;

namespace HospitalityOS.Ports.Storage;

/// <summary>
/// What every <see cref="IBlobStore"/> accepts as a key, so a key works on every cloud and file system: lowercase
/// segments of letters, digits, <c>.</c>, <c>_</c>, and <c>-</c>, each starting with a letter or digit, joined by
/// <c>/</c>, at most <see cref="MaxLength"/> characters.
/// </summary>
public static partial class BlobKeys
{
    /// <summary>
    /// The longest key.
    /// </summary>
    public const int MaxLength = 512;

    /// <summary>
    /// Whether <paramref name="key"/> is a valid blob key.
    /// </summary>
    public static bool IsValid(string? key) => key is { Length: > 0 and <= MaxLength } && Pattern().IsMatch(key);

    /// <summary>
    /// Throws unless <paramref name="key"/> is a valid blob key.
    /// </summary>
    /// <exception cref="ArgumentException">The key is not valid.</exception>
    public static void EnsureValid(string? key)
    {
        if (!IsValid(key))
        {
            throw new ArgumentException($"'{key}' is not a blob key: lowercase segments of letters, digits, '.', '_', and '-', joined by '/', at most {MaxLength} characters.", nameof(key));
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*(/[a-z0-9][a-z0-9._-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
