using HospitalityOS.Application.Access;
using HospitalityOS.Application.Conversations;
using HospitalityOS.Domain.Guests.GuestLinks;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.GuestLinks;

internal sealed class GuestSessions(IGuestLinkDirectory directory, IGuestLinkRepository links, IStayContextRepository stays, TenantContext tenantContext) : IGuestSessions
{
    public static Error LinkNotFound { get; } = Error.NotFound("guest_link.not_found", "This link does not open any stay. Ask the front desk for a new one.");

    public async Task<Result<GuestSession>> OpenAsync(string? code, CancellationToken cancellationToken)
    {
        if (InviteCodes.Hash(code) is not { } hash || await directory.FindByCodeAsync(hash, cancellationToken).ConfigureAwait(false) is not { } entry)
        {
            return LinkNotFound;
        }

        if (tenantContext.IsSet && tenantContext.TenantId != entry.TenantId)
        {
            return LinkNotFound;
        }

        tenantContext.Set(entry.TenantId);

        if (await links.GetByIdAsync(entry.LinkId, cancellationToken).ConfigureAwait(false) is not { IsOpen: true } link)
        {
            return LinkNotFound;
        }

        StayContext? stay = await stays.GetByIdAsync(link.StayContextId, cancellationToken).ConfigureAwait(false);
        GuestCaller caller = new(link.GuestId, link.StayContextId, link.PropertyId);

        if (stay is null)
        {
            return LinkNotFound;
        }

        return caller.IsActive(stay) ? new GuestSession(entry.TenantId, caller, link.Id) : GuestLink.StayEnded(stay);
    }

    public async Task<bool> IsOpenAsync(GuestLinkId linkId, GuestCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        GuestLink? link = await links.GetByIdAsync(linkId, cancellationToken).ConfigureAwait(false);

        if (link is not { IsOpen: true } || link.StayContextId != caller.StayContextId || link.GuestId != caller.GuestId)
        {
            return false;
        }

        return caller.IsActive(await stays.GetByIdAsync(link.StayContextId, cancellationToken).ConfigureAwait(false));
    }
}
