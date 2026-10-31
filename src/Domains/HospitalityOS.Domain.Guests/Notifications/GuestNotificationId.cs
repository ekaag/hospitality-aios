using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.Notifications;

/// <summary>
/// Identifies a <see cref="GuestNotification"/>.
/// </summary>
public readonly record struct GuestNotificationId : ITypedId
{
    /// <summary>
    /// Creates a guest notification ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public GuestNotificationId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(GuestNotificationId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
