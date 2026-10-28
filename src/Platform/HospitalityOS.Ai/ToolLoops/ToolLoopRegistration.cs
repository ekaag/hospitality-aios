using System.Globalization;
using System.Text.RegularExpressions;

namespace HospitalityOS.Ai.ToolLoops;

/// <summary>
/// A tool loop as declared with <c>AddToolLoop</c>: its chat task, its allow-list of capabilities at pinned versions,
/// and how many rounds of tool calls it may make.
/// </summary>
internal sealed partial record ToolLoopRegistration(string Task, IReadOnlyList<(string Name, int Version)> Tools, int MaxToolRounds)
{
    public const int DefaultMaxToolRounds = 5;
    public const int MostToolRounds = 20;

    /// <exception cref="ArgumentException">The task is blank, there are no tools, an entry is not
    /// <c>Area.Action@version</c>, or the round limit is out of range.</exception>
    public static ToolLoopRegistration Create(string task, IEnumerable<string> tools, int maxToolRounds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxToolRounds, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxToolRounds, MostToolRounds);

        List<(string Name, int Version)> allowed = [];

        foreach (string entry in tools)
        {
            Match match = Entry().Match(entry ?? "");

            if (!match.Success)
            {
                throw new ArgumentException($"Tool '{entry}' must name a capability and its version, e.g. Property.GetOperatingHours@1.", nameof(tools));
            }

            allowed.Add((match.Groups["name"].Value, int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture)));
        }

        if (allowed.Count == 0)
        {
            throw new ArgumentException($"Tool loop '{task}' must allow at least one tool.", nameof(tools));
        }

        return new ToolLoopRegistration(task, allowed, maxToolRounds);
    }

    [GeneratedRegex("^(?<name>[A-Z][A-Za-z0-9]*\\.[A-Z][A-Za-z0-9]*)@(?<version>[1-9][0-9]{0,5})$", RegexOptions.CultureInvariant)]
    private static partial Regex Entry();
}
