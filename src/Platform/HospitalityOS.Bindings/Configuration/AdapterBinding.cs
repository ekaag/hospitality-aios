using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Bindings.Configuration;

/// <summary>
/// One configured binding: which adapter to use and its settings.
/// </summary>
/// <param name="Path">Configuration path of the binding, e.g. <c>Bindings:EventTransport:Default</c>. Unique per binding.</param>
/// <param name="Adapter">Adapter name to select a factory by.</param>
/// <param name="Settings">The binding's <c>Settings</c> section (may be empty).</param>
internal sealed record AdapterBinding(string Path, string Adapter, IConfiguration Settings)
{
    public const string AdapterKey = "Adapter";
    public const string SettingsKey = "Settings";

    public static AdapterBinding? FromSection(IConfigurationSection section)
    {
        if (!section.GetChildren().Any())
        {
            return null;
        }

        return new AdapterBinding(section.Path, section[AdapterKey] ?? string.Empty, section.GetSection(SettingsKey));
    }
}
