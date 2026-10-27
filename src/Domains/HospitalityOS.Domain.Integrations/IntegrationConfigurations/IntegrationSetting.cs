namespace HospitalityOS.Domain.Integrations.IntegrationConfigurations;

/// <summary>
/// One provider setting, e.g. <c>hotelCode</c> = <c>AG01</c>. Never a secret: a secret is referenced by its name in the
/// secret store (P10).
/// </summary>
/// <param name="Name">The setting's name; unique within a configuration, ignoring case.</param>
/// <param name="Value">The setting's value.</param>
public sealed record IntegrationSetting(string Name, string Value);
