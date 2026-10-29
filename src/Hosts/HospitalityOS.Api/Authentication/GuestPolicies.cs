namespace HospitalityOS.Api.Authentication;

/// <summary>
/// What guests may call: only the guest-facing endpoints, with a stay token. Staff tokens are refused there, and stay
/// tokens everywhere else (spec §37 role-based access).
/// </summary>
internal static class GuestPolicies
{
    public const string Guest = nameof(Guest);

    public static void Add(Microsoft.AspNetCore.Authorization.AuthorizationOptions options) =>
        options.AddPolicy(Guest, policy => policy.RequireRole(GuestClaims.Role));
}
