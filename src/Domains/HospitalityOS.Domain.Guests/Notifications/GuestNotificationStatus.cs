namespace HospitalityOS.Domain.Guests.Notifications;

/// <summary>
/// Where a <see cref="GuestNotification"/> is: Requested → Sent, or Requested → Failed. Sent and Failed are final.
/// </summary>
public enum GuestNotificationStatus
{
    /// <summary>
    /// Recorded; not yet accepted by the messaging provider.
    /// </summary>
    Requested,

    /// <summary>
    /// The messaging provider accepted it for delivery. Final.
    /// </summary>
    Sent,

    /// <summary>
    /// It could not be sent, e.g. the provider refused the recipient or none is configured. Final.
    /// </summary>
    Failed,
}
