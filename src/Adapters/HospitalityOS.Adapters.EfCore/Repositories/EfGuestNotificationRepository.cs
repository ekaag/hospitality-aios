using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Guests.Notifications;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Adapters.EfCore.Repositories;

internal sealed class EfGuestNotificationRepository(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    : EfRepository<GuestNotification, GuestNotificationId>(context, unitOfWork, currentTenant), IGuestNotificationRepository
{
    public Task<GuestNotification?> FindByKeyAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        string trimmed = key.Trim();

        return FindFirstAsync(notification => notification.Key == trimmed, cancellationToken);
    }
}
