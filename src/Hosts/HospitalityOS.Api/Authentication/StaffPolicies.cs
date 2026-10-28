using HospitalityOS.SharedKernel.Execution;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// Who may call what (spec §37 role-based access): every staff role reads; managers and administrators oversee AI
/// activity; only administrators change configuration.
/// </summary>
internal static class StaffPolicies
{
    public const string Staff = nameof(Staff);
    public const string Oversight = nameof(Oversight);
    public const string Admin = nameof(Admin);

    public static void Add(Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
    {
        options.AddPolicy(Staff, policy => policy.RequireRole(Enum.GetNames<StaffRole>()));
        options.AddPolicy(Oversight, policy => policy.RequireRole(nameof(StaffRole.Manager), nameof(StaffRole.Admin)));
        options.AddPolicy(Admin, policy => policy.RequireRole(nameof(StaffRole.Admin)));
    }
}
