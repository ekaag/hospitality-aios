namespace HospitalityOS.Domain.Integrations.IntegrationConfigurations;

/// <summary>
/// Whether an <see cref="IntegrationConfiguration"/> may be used.
/// </summary>
public enum IntegrationStatus
{
    /// <summary>
    /// The connector serves the property.
    /// </summary>
    Active,

    /// <summary>
    /// Switched off; resolving the connector fails until it is enabled again.
    /// </summary>
    Disabled,
}
