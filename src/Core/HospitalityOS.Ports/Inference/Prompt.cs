using System.Text.RegularExpressions;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// One version of a prompt (P6): provider-independent text with <c>{{variable}}</c> placeholders the caller fills in.
/// </summary>
/// <param name="Version">Which prompt and version this is.</param>
/// <param name="Template">The text, with LF line endings.</param>
public sealed partial record Prompt(PromptVersion Version, string Template)
{
    /// <summary>
    /// The placeholders' names, distinct, in order of first use.
    /// </summary>
    public IReadOnlyList<string> Variables => [.. Placeholder().Matches(Template).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The text with every placeholder replaced by its value. Values are inserted as they are: a value containing
    /// <c>{{name}}</c> (e.g. from a guest) is not filled in again.
    /// </summary>
    /// <param name="values">A value for each of <see cref="Variables"/>, by name; no others.</param>
    /// <exception cref="ArgumentException">A variable has no value, or a value names no variable.</exception>
    public string Render(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        IReadOnlyList<string> variables = Variables;
        string[] missing = [.. variables.Where(variable => !values.ContainsKey(variable))];
        string[] unknown = [.. values.Keys.Where(name => !variables.Contains(name, StringComparer.Ordinal))];

        if (missing.Length > 0 || unknown.Length > 0)
        {
            throw new ArgumentException(
                $"Prompt '{Version}' needs values for exactly: {Describe(variables)}. Missing: {Describe(missing)}. Unknown: {Describe(unknown)}.",
                nameof(values));
        }

        return Placeholder().Replace(Template, match => values[match.Groups[1].Value]);
    }

    private static string Describe(IReadOnlyCollection<string> names) => names.Count == 0 ? "(none)" : string.Join(", ", names);

    [GeneratedRegex(@"\{\{([A-Za-z][A-Za-z0-9_]*)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
