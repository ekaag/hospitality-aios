using System.Security.Claims;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// The claims a staff token must carry (P10): <c>sub</c> (the staff member), <c>tenant_id</c> (a GUID), and at least one
/// <c>role</c> of <see cref="StaffRole"/>. A token missing any of them is rejected outright, so no request runs without
/// a tenant.
/// </summary>
internal static class StaffClaims
{
    public const string Subject = "sub";
    public const string Tenant = "tenant_id";
    public const string Role = "role";

    /// <summary>
    /// Reads the caller from a validated token; the highest role wins when several are present.
    /// </summary>
    /// <returns>Whether the claims are complete; <paramref name="problem"/> says what is wrong when not.</returns>
    public static bool TryRead(ClaimsPrincipal principal, out TenantId tenant, out Actor actor, out string? problem)
    {
        tenant = default;
        actor = Actor.System;
        string? subject = principal.FindFirst(Subject)?.Value;
        StaffRole[] roles = [.. principal.FindAll(Role).Select(claim => Enum.TryParse(claim.Value, ignoreCase: false, out StaffRole role) && Enum.IsDefined(role) ? role : (StaffRole?)null).OfType<StaffRole>()];

        problem = string.IsNullOrWhiteSpace(subject) ? $"The token has no '{Subject}' claim."
            : !Guid.TryParse(principal.FindFirst(Tenant)?.Value, out Guid tenantId) || tenantId == Guid.Empty ? $"The token has no valid '{Tenant}' claim."
            : roles.Length == 0 ? $"The token has no '{Role}' claim naming a staff role ({string.Join(", ", Enum.GetNames<StaffRole>())})."
            : null;

        if (problem is not null)
        {
            return false;
        }

        tenant = new TenantId(Guid.Parse(principal.FindFirst(Tenant)!.Value));
        actor = Actor.Staff(subject!, roles.Max());

        return true;
    }
}
