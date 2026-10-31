using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Guests.Notifications;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.InMemory.Repositories;

internal sealed class InMemoryGuestNotificationRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : InMemoryRepository<GuestNotification, GuestNotificationId>(store, unitOfWork, currentTenant), IGuestNotificationRepository
{
    public Task<GuestNotification?> FindByKeyAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        string trimmed = key.Trim();

        return FindFirstAsync(notification => notification.Key == trimmed, cancellationToken);
    }
}
