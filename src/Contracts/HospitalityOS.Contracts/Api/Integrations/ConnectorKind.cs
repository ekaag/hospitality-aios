namespace HospitalityOS.Contracts.Api.Integrations;

/// <summary>
/// The external systems a property connects to (spec §10).
/// </summary>
public enum ConnectorKind
{
    /// <summary>
    /// Property management: reservations and stays.
    /// </summary>
    Pms,

    /// <summary>
    /// Content management.
    /// </summary>
    Cms,

    /// <summary>
    /// Point of sale.
    /// </summary>
    Pos,

    /// <summary>
    /// Messaging guests (SMS, WhatsApp, the web chat).
    /// </summary>
    Messaging,

    /// <summary>
    /// Central reservations.
    /// </summary>
    Crs,

    /// <summary>
    /// Payments.
    /// </summary>
    Payment,
}
