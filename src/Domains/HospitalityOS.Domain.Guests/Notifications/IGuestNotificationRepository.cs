namespace HospitalityOS.Domain.Guests.Notifications;

/// <summary>
/// Persistence port for <see cref="GuestNotification"/>. Implementations are scoped to the current tenant (P10) and commit
/// through the unit of work.
/// </summary>
public interface IGuestNotificationRepository
{
    /// <summary>
    /// Loads a notification, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<GuestNotification?> GetByIdAsync(GuestNotificationId id, CancellationToken cancellationToken);

    /// <summary>
    /// The notification with <paramref name="key"/>, if one was requested: a key is one logical message (P8).
    /// </summary>
    Task<GuestNotification?> FindByKeyAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new notification, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(GuestNotification notification, CancellationToken cancellationToken);
}
