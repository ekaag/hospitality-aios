namespace HospitalityOS.Contracts.Api.Integrations;

/// <summary>
/// Whether a property uses one of its connectors.
/// </summary>
public enum ConnectorStatus
{
    /// <summary>
    /// No provider has been chosen.
    /// </summary>
    NotConfigured,

    /// <summary>
    /// In use.
    /// </summary>
    Active,

    /// <summary>
    /// Configured but switched off.
    /// </summary>
    Disabled,
}
