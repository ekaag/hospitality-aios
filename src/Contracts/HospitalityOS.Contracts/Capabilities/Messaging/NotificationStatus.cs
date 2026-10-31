namespace HospitalityOS.Contracts.Capabilities.Messaging;

/// <summary>
/// Where a message to a guest is.
/// </summary>
public enum NotificationStatus
{
    /// <summary>
    /// Recorded; not yet accepted by the messaging provider.
    /// </summary>
    Requested,

    /// <summary>
    /// The messaging provider accepted it for delivery.
    /// </summary>
    Sent,

    /// <summary>
    /// It could not be sent; the guest was not told.
    /// </summary>
    Failed,
}
