namespace HospitalityOS.Domain.Integrations.IntegrationConfigurations;

/// <summary>
/// The kinds of external system a property can connect (spec §10), one connector interface each.
/// </summary>
public enum ConnectorKind
{
    /// <summary>
    /// Property management system (<c>IPmsConnector</c>).
    /// </summary>
    Pms,

    /// <summary>
    /// Content management system (<c>ICmsConnector</c>).
    /// </summary>
    Cms,

    /// <summary>
    /// Point of sale (<c>IPosConnector</c>).
    /// </summary>
    Pos,

    /// <summary>
    /// Guest messaging (<c>IMessagingConnector</c>).
    /// </summary>
    Messaging,

    /// <summary>
    /// Central reservation system (<c>ICrsConnector</c>); placeholder.
    /// </summary>
    Crs,

    /// <summary>
    /// Payment provider (<c>IPaymentConnector</c>); placeholder.
    /// </summary>
    Payment,
}
