using HospitalityOS.Contracts.Api.DevTokens;
using HospitalityOS.Seeding;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// The people of the reference hotels' seed a local developer signs in as (J1): at the Aurora Grand, Alex, the in-house
/// guest in room 412, and three workers on the roster; at the Mysore Grand, Ananya, in house in room 210, and its three
/// workers. Workers' staff member IDs are their subjects, so the work routed to them is theirs to accept. The manager and
/// administrator work across both hotels. The tokens only make sense with the seed loaded.
/// </summary>
internal static class DevPersonas
{
    public static IReadOnlyList<DevPersonaClaims> All { get; } =
    [
        Guest("guest-alex", "Alex Morgan", "The in-house guest in room 412.", AuroraGrand.Alex, AuroraGrand.AlexStay, AuroraGrand.PropertyId, AuroraGrand.Name),
        Staff("worker-sam", "Sam Patel", "Housekeeping, on duty.", AuroraGrand.SamPatel.Value.ToString(), StaffRole.Worker, AuroraGrand.PropertyId, AuroraGrand.Name),
        Staff("worker-jo", "Jo Okafor", "Housekeeping, on duty.", AuroraGrand.JoOkafor.Value.ToString(), StaffRole.Worker, AuroraGrand.PropertyId, AuroraGrand.Name),
        Staff("worker-ravi", "Ravi Mehta", "Maintenance, on duty.", AuroraGrand.RaviMehta.Value.ToString(), StaffRole.Worker, AuroraGrand.PropertyId, AuroraGrand.Name),
        Guest("guest-ananya", "Ananya Rao", "The in-house guest in room 210.", MysoreGrand.Ananya, MysoreGrand.AnanyaStay, MysoreGrand.PropertyId, MysoreGrand.Name),
        Staff("worker-lakshmi", "Lakshmi Iyer", "Housekeeping, on duty.", MysoreGrand.LakshmiIyer.Value.ToString(), StaffRole.Worker, MysoreGrand.PropertyId, MysoreGrand.Name),
        Staff("worker-kiran", "Kiran Gowda", "Housekeeping, on duty.", MysoreGrand.KiranGowda.Value.ToString(), StaffRole.Worker, MysoreGrand.PropertyId, MysoreGrand.Name),
        Staff("worker-arjun", "Arjun Nair", "Maintenance, on duty.", MysoreGrand.ArjunNair.Value.ToString(), StaffRole.Worker, MysoreGrand.PropertyId, MysoreGrand.Name),
        Staff("manager", "Dana Reyes", "Duty manager: oversees work and AI activity in both hotels.", "dev-manager", StaffRole.Manager, null, null),
        Staff("admin", "Priya Shah", "Administrator: configures both hotels.", "dev-admin", StaffRole.Admin, null, null),
    ];

    public static DevPersonaClaims? Find(string? id) => All.FirstOrDefault(persona => persona.Persona.Id == id?.Trim());

    private static DevPersonaClaims Guest(string id, string name, string description, GuestId guest, StayContextId stay, PropertyId property, string propertyName) => new(
        new DevPersona(id, name, description, GuestClaims.Role, property.Value, propertyName),
        new Dictionary<string, object>
        {
            [StaffClaims.Subject] = guest.Value.ToString(),
            [StaffClaims.Tenant] = AuroraGrand.TenantId.Value.ToString(),
            [StaffClaims.Role] = GuestClaims.Role,
            [GuestClaims.Property] = property.Value.ToString(),
            [GuestClaims.Stay] = stay.Value.ToString(),
        });

    private static DevPersonaClaims Staff(string id, string name, string description, string subject, StaffRole role, PropertyId? property, string? propertyName) => new(
        new DevPersona(id, name, description, role.ToString(), property?.Value, propertyName),
        new Dictionary<string, object>
        {
            [StaffClaims.Subject] = subject,
            [StaffClaims.Tenant] = AuroraGrand.TenantId.Value.ToString(),
            [StaffClaims.Role] = role.ToString(),
        });
}

/// <summary>
/// A persona and the claims of its token.
/// </summary>
internal sealed record DevPersonaClaims(DevPersona Persona, IReadOnlyDictionary<string, object> Claims);
