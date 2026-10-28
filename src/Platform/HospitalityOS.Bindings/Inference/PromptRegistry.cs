using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HospitalityOS.Ports.Inference;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// Prompts embedded in the assemblies registered with <c>AddPrompts</c> (P6). Each version's SHA-256 must be recorded in
/// its assembly's lock file, so a released version cannot change unnoticed: an edit has to become a new version.
/// Problems (an unlocked or changed version, a gap in the numbering, a malformed file name or lock file, a name embedded
/// twice) are collected in <see cref="Errors"/> for startup validation instead of thrown. Singleton.
/// </summary>
internal sealed partial class PromptRegistry : IPromptRegistry
{
    private readonly Dictionary<string, (string Origin, SortedList<int, Prompt> Versions)> prompts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> errors = [];

    public PromptRegistry(IEnumerable<PromptSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        foreach (PromptSource source in sources.DistinctBy(source => source.Origin, StringComparer.Ordinal))
        {
            Load(source);
        }

        foreach ((string name, (_, SortedList<int, Prompt> versions)) in prompts)
        {
            if (versions.Keys[^1] != versions.Count)
            {
                errors.Add($"Prompt '{name}' has versions {string.Join(", ", versions.Keys)}; versions are numbered from 1 without gaps.");
            }
        }
    }

    public IReadOnlyList<string> Errors => errors;

    public Prompt? Find(string name, int? number = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!prompts.TryGetValue(name, out (string Origin, SortedList<int, Prompt> Versions) prompt))
        {
            return null;
        }

        return number is { } version ? prompt.Versions.GetValueOrDefault(version) : prompt.Versions.Values[^1];
    }

    /// <summary>
    /// The text as hashed and served: without a byte order mark, with LF line endings, so the hash does not depend on how
    /// the file was checked out.
    /// </summary>
    internal static string Normalize(string text) => text.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    internal static string Hash(string normalized) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));

    private void Load(PromptSource source)
    {
        Dictionary<string, string> locked = ReadLock(source);
        HashSet<string> found = new(StringComparer.OrdinalIgnoreCase);

        foreach (PromptFile file in source.Files)
        {
            Match match = FileName().Match(file.Path);

            if (!match.Success)
            {
                errors.Add($"'Prompts/{file.Path}' in {source.Origin} is not named Prompts/{{name}}/v{{number}}.md (name in lowercase letters, digits, '.', '-', '_'; number from 1).");
                continue;
            }

            string name = match.Groups["name"].Value;
            int number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
            string template = Normalize(file.Text);
            PromptVersion version = new(name, number, Hash(template));
            found.Add(version.ToString());

            if (string.IsNullOrWhiteSpace(template))
            {
                errors.Add($"Prompt '{version}' in {source.Origin} is empty.");
            }

            if (!locked.TryGetValue(version.ToString(), out string? lockedHash))
            {
                errors.Add($"Prompt '{version}' in {source.Origin} is not locked. Once it is final, add \"{version}\": \"{version.Hash}\" to Prompts/{PromptSource.LockFileName}.");
            }
            else if (!string.Equals(lockedHash, version.Hash, StringComparison.Ordinal))
            {
                errors.Add($"Prompt '{version}' in {source.Origin} has changed since it was locked ({lockedHash}, now {version.Hash}). Released versions never change: restore it and add the change as a new version.");
            }

            Add(source.Origin, new Prompt(version, template));
        }

        foreach (string entry in locked.Keys.Where(entry => !found.Contains(entry)).Order(StringComparer.Ordinal))
        {
            errors.Add($"Prompts/{PromptSource.LockFileName} in {source.Origin} locks '{entry}', which has no file.");
        }
    }

    private void Add(string origin, Prompt prompt)
    {
        string name = prompt.Version.Name;

        if (!prompts.TryGetValue(name, out (string Origin, SortedList<int, Prompt> Versions) existing))
        {
            prompts[name] = (origin, new SortedList<int, Prompt> { [prompt.Version.Number] = prompt });
        }
        else if (existing.Origin != origin)
        {
            errors.Add($"Prompt '{name}' is embedded in both {existing.Origin} and {origin}; a prompt has one home.");
        }
        else
        {
            existing.Versions[prompt.Version.Number] = prompt;
        }
    }

    private Dictionary<string, string> ReadLock(PromptSource source)
    {
        Dictionary<string, string> locked = new(StringComparer.OrdinalIgnoreCase);

        if (source.Lock is null)
        {
            return locked;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(source.Lock);

            foreach (JsonProperty entry in document.RootElement.EnumerateObject())
            {
                locked[entry.Name] = entry.Value.GetString() ?? "";
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            errors.Add($"Prompts/{PromptSource.LockFileName} in {source.Origin} must be a JSON object of \"name@number\": \"sha256:...\" entries: {exception.Message}");
        }

        return locked;
    }

    [GeneratedRegex(@"^(?<name>[a-z0-9][a-z0-9._-]*)/v(?<number>[1-9][0-9]{0,5})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex FileName();
}
