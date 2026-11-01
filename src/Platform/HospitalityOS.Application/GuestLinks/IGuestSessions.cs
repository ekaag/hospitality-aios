using HospitalityOS.Application.Conversations;
using HospitalityOS.Domain.Guests.GuestLinks;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Application.GuestLinks;

/// <summary>
/// Guests opening sessions from check-in links (K4c): a code names its link across every tenant; an open link of a
/// reserved or in-house stay gives the guest to issue a stay token for. Not audited: sessions open often and change
/// nothing. Scoped; opening sets the scope's tenant, so the scope must not have acted for another.
/// </summary>
public interface IGuestSessions
{
    /// <summary>
    /// The guest a code lets in.
    /// </summary>
    /// <returns>The session; <c>guest_link.not_found</c> for a code that opens no open link; or
    /// <c>guest_link.stay_ended</c>.</returns>
    Task<Result<GuestSession>> OpenAsync(string? code, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a session's link is still open and its stay still reserved or in house, in the scope's tenant, so a
    /// revoked link or a finished stay ends the sessions it opened at once.
    /// </summary>
    Task<bool> IsOpenAsync(GuestLinkId linkId, GuestCaller caller, CancellationToken cancellationToken);
}

/// <summary>
/// Who a session is for: the tenant, the guest and their stay, and the link that opened it.
/// </summary>
public sealed record GuestSession(TenantId TenantId, GuestCaller Caller, GuestLinkId LinkId);
