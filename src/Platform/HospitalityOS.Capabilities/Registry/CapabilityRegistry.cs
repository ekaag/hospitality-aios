using HospitalityOS.Capabilities.Definitions;

namespace HospitalityOS.Capabilities.Registry;

/// <summary>
/// Every registered capability, by name and version. Anything not here cannot be invoked (P7): agents get their
/// allow-lists from it (G4) and the executor looks capabilities up in it (D2). Singleton.
/// </summary>
public sealed class CapabilityRegistry
{
    private readonly Dictionary<string, CapabilityDefinition[]> versionsByName = new(StringComparer.Ordinal);

    /// <summary>
    /// Builds the registry from the definitions registered with <c>AddCapability</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two definitions share a name and version.</exception>
    public CapabilityRegistry(IEnumerable<CapabilityDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        All = definitions
            .OrderBy(definition => definition.Name, StringComparer.Ordinal)
            .ThenBy(definition => definition.Version)
            .ToList();

        foreach (IGrouping<string, CapabilityDefinition> named in All.GroupBy(definition => definition.Name, StringComparer.Ordinal))
        {
            CapabilityDefinition[] versions = named.ToArray();

            if (versions.DistinctBy(definition => definition.Version).Count() != versions.Length)
            {
                throw new InvalidOperationException($"Capability {named.Key} has a version registered more than once.");
            }

            versionsByName.Add(named.Key, versions);
        }
    }

    /// <summary>
    /// Every definition, by name (ordinal) and then version.
    /// </summary>
    public IReadOnlyList<CapabilityDefinition> All { get; }

    /// <summary>
    /// The capability named exactly <paramref name="name"/> (case-sensitive) at <paramref name="version"/>, or at its
    /// latest version when none is given; <see langword="null"/> when there is none.
    /// </summary>
    public CapabilityDefinition? Find(string name, int? version = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!versionsByName.TryGetValue(name, out CapabilityDefinition[]? versions))
        {
            return null;
        }

        return version is null ? versions[^1] : versions.FirstOrDefault(definition => definition.Version == version);
    }
}
