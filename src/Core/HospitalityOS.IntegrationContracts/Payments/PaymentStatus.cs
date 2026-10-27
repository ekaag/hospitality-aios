namespace HospitalityOS.IntegrationContracts.Payments;

/// <summary>
/// Lifecycle of a <see cref="PaymentRecord"/>.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Started, not yet authorized.
    /// </summary>
    Pending,

    /// <summary>
    /// Funds reserved, not yet taken.
    /// </summary>
    Authorized,

    /// <summary>
    /// Funds taken.
    /// </summary>
    Captured,

    /// <summary>
    /// Funds returned after capture.
    /// </summary>
    Refunded,

    /// <summary>
    /// Declined or errored.
    /// </summary>
    Failed,

    /// <summary>
    /// Called off before capture.
    /// </summary>
    Cancelled,
}
