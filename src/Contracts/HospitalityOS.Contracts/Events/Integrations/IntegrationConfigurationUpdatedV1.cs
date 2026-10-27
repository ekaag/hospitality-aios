namespace HospitalityOS.Contracts.Events.Integrations;

/// <summary>
/// Version 1 of <c>IntegrationConfigurationUpdated</c>: a connector configuration changed provider, settings, or status.
/// Settings are not published.
/// </summary>
/// <param name="IntegrationConfigurationId">The configuration.</param>
/// <param name="Kind">The connector kind (domain enum name).</param>
/// <param name="ProviderName">The provider after the change.</param>
/// <param name="Status">The status after the change (domain enum name).</param>
public sealed record IntegrationConfigurationUpdatedV1(
    Guid IntegrationConfigurationId,
    string Kind,
    string ProviderName,
    string Status) : IEventPayload;
