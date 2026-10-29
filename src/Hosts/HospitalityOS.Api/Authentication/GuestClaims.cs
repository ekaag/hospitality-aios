using System.Security.Claims;
using HospitalityOS.Application.Conversations;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// The claims of a guest's stay token (P10), from the same trusted issuer as staff tokens: <c>role</c> <see cref="Role"/>
/// and no other role, <c>sub</c> (the guest's ID), <c>tenant_id</c>, <c>property_id</c>, and <c>stay_id</c>, all GUIDs.
/// Locally, <c>dotnet user-jwts create --role Guest --name &lt;guest-id&gt; --claim tenant_id=&lt;guid&gt;
/// --claim property_id=&lt;guid&gt; --claim stay_id=&lt;guid&gt;</c> issues one.
/// </summary>
internal static class GuestClaims
{
    public const string Role = "Guest";
    public const string Property = "property_id";
    public const string Stay = "stay_id";

    /// <summary>
    /// Whether the token claims to be a guest's, so it is read as one rather than as a staff token.
    /// </summary>
    public static bool IsGuest(ClaimsPrincipal principal) => principal.HasClaim(StaffClaims.Role, Role);

    /// <summary>
    /// Reads the guest from a validated token.
    /// </summary>
    /// <returns>Whether the claims are complete; <paramref name="problem"/> says what is wrong when not.</returns>
    public static bool TryRead(ClaimsPrincipal principal, out TenantId tenant, out GuestCaller? caller, out string? problem)
    {
        tenant = default;
        caller = null;
        Guid? guestId = IdClaim(principal, StaffClaims.Subject);
        Guid? tenantId = IdClaim(principal, StaffClaims.Tenant);
        Guid? propertyId = IdClaim(principal, Property);
        Guid? stayId = IdClaim(principal, Stay);

        problem = principal.FindAll(StaffClaims.Role).Any(claim => claim.Value != Role) ? $"A guest token has no role but '{Role}'."
            : guestId is null ? $"The token has no '{StaffClaims.Subject}' claim with the guest's ID."
            : tenantId is null ? $"The token has no valid '{StaffClaims.Tenant}' claim."
            : propertyId is null ? $"The token has no valid '{Property}' claim."
            : stayId is null ? $"The token has no valid '{Stay}' claim."
            : null;

        if (problem is not null)
        {
            return false;
        }

        tenant = new TenantId(tenantId!.Value);
        caller = new GuestCaller(new GuestId(guestId!.Value), new StayContextId(stayId!.Value), new PropertyId(propertyId!.Value));

        return true;
    }

    /// <summary>
    /// The actor a guest acts as: events and audit records name the guest's ID.
    /// </summary>
    public static Actor Actor(GuestCaller caller) => new(ActorType.Guest, caller.GuestId.Value.ToString());

    private static Guid? IdClaim(ClaimsPrincipal principal, string type) =>
        Guid.TryParse(principal.FindFirst(type)?.Value, out Guid id) && id != Guid.Empty ? id : null;
}
