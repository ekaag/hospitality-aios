using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Bindings;

/// <summary>
/// The <c>Bindings</c> configuration section, registered once by <c>AddBindings</c>.
/// </summary>
/// <param name="Section">The configuration section holding all port bindings.</param>
internal sealed record BindingRoot(IConfigurationSection Section)
{
    public const string SectionName = "Bindings";
}
