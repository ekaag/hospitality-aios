using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// A guest and their stay at a property, saved in the host, for stay tokens.
/// </summary>
internal sealed record GuestStay(GuestId GuestId, StayContextId StayContextId, PropertyId PropertyId)
{
    /// <summary>
    /// Registers a guest with a stay at <paramref name="propertyId"/> that is in house (in a room known by ID only), or with
    /// <paramref name="status"/>.
    /// </summary>
    public static async Task<GuestStay> SaveAsync(
        IServiceProvider services,
        TenantId tenant,
        PropertyId propertyId,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        StayStatus status = StayStatus.InHouse)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        Guest guest = Guest.Register(new GuestId(Guid.CreateVersion7()), tenant, new GuestProfile("Alex", "en"), now).Value;
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        StayContext stay = StayContext.Create(new StayContextId(Guid.CreateVersion7()), guest, propertyId, today.AddDays(-1), today.AddDays(2), null, now).Value;

        if (status is StayStatus.InHouse or StayStatus.CheckedOut)
        {
            Assert.True(stay.CheckIn(new AccommodationUnitId(Guid.CreateVersion7()), now).IsSuccess);
        }

        if (status is StayStatus.CheckedOut)
        {
            Assert.True(stay.CheckOut(now).IsSuccess);
        }

        await scope.ServiceProvider.GetRequiredService<IGuestRepository>().AddAsync(guest, cancellationToken);
        await scope.ServiceProvider.GetRequiredService<IStayContextRepository>().AddAsync(stay, cancellationToken);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(cancellationToken)).IsSuccess);

        return new GuestStay(guest.Id, stay.Id, propertyId);
    }
}
