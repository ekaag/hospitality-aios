using HospitalityOS.Contracts.Api.DevTokens;
using HospitalityOS.SharedKernel.Execution;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// The people of the Aurora Grand seed a local developer signs in as (J1): Alex, the in-house guest in room 412; the three
/// workers on the roster, whose staff member IDs are their subjects so the work routed to them is theirs to accept; and a
/// manager and an administrator. The tokens only make sense with the seed loaded.
/// </summary>
internal static class DevPersonas
{
    public static IReadOnlyList<DevPersonaClaims> All { get; } =
    [
        Guest("guest-alex", "Alex Morgan", "The in-house guest in room 412."),
        Staff("worker-sam", "Sam Patel", "Housekeeping, on duty.", Seed.SamPatel.Value.ToString(), StaffRole.Worker),
        Staff("worker-jo", "Jo Okafor", "Housekeeping, on duty.", Seed.JoOkafor.Value.ToString(), StaffRole.Worker),
        Staff("worker-ravi", "Ravi Mehta", "Maintenance, on duty.", Seed.RaviMehta.Value.ToString(), StaffRole.Worker),
        Staff("manager", "Dana Reyes", "Duty manager: oversees work and AI activity.", "dev-manager", StaffRole.Manager),
        Staff("admin", "Priya Shah", "Administrator: configures the property.", "dev-admin", StaffRole.Admin),
    ];

    public static DevPersonaClaims? Find(string? id) => All.FirstOrDefault(persona => persona.Persona.Id == id?.Trim());

    private static DevPersonaClaims Guest(string id, string name, string description) => new(
        new DevPersona(id, name, description, GuestClaims.Role),
        new Dictionary<string, object>
        {
            [StaffClaims.Subject] = Seed.Alex.Value.ToString(),
            [StaffClaims.Tenant] = Seed.TenantId.Value.ToString(),
            [StaffClaims.Role] = GuestClaims.Role,
            [GuestClaims.Property] = Seed.PropertyId.Value.ToString(),
            [GuestClaims.Stay] = Seed.AlexStay.Value.ToString(),
        });

    private static DevPersonaClaims Staff(string id, string name, string description, string subject, StaffRole role) => new(
        new DevPersona(id, name, description, role.ToString()),
        new Dictionary<string, object>
        {
            [StaffClaims.Subject] = subject,
            [StaffClaims.Tenant] = Seed.TenantId.Value.ToString(),
            [StaffClaims.Role] = role.ToString(),
        });
}

/// <summary>
/// A persona and the claims of its token.
/// </summary>
internal sealed record DevPersonaClaims(DevPersona Persona, IReadOnlyDictionary<string, object> Claims);
