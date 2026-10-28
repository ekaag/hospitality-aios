namespace HospitalityOS.Application.Administration;

/// <summary>
/// The audited names of administrative actions; endpoints use the same names, so a denied attempt is recorded under the
/// action it tried.
/// </summary>
public static class AdministrationActions
{
    public const string OrganizationCreate = "Organization.Create";
    public const string OrganizationRename = "Organization.Rename";
    public const string PropertyCreate = "Property.Create";
    public const string PropertyUpdate = "Property.Update";
}
