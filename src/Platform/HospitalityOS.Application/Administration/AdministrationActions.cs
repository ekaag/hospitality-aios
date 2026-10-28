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
    public const string SpaceCreate = "Space.Create";
    public const string SpaceUpdate = "Space.Update";
    public const string AmenityCreate = "Amenity.Create";
    public const string AmenityUpdate = "Amenity.Update";
    public const string OperatingHoursCreate = "OperatingHours.Create";
    public const string OperatingHoursUpdate = "OperatingHours.Update";
    public const string PolicyCreate = "Policy.Create";
    public const string PolicyUpdate = "Policy.Update";
    public const string PolicyActivate = "Policy.Activate";
    public const string PolicyRetire = "Policy.Retire";
    public const string ContentItemCreate = "ContentItem.Create";
    public const string ContentItemUpdate = "ContentItem.Update";
    public const string ContentItemPublish = "ContentItem.Publish";
    public const string ContentItemArchive = "ContentItem.Archive";
    public const string RestaurantCreate = "Restaurant.Create";
    public const string RestaurantUpdate = "Restaurant.Update";
    public const string MenuCreate = "Menu.Create";
    public const string MenuUpdate = "Menu.Update";
    public const string MenuSetItemAvailability = "Menu.SetItemAvailability";
    public const string PropertyEventCreate = "PropertyEvent.Create";
    public const string PropertyEventUpdate = "PropertyEvent.Update";
    public const string PropertyEventCancel = "PropertyEvent.Cancel";
    public const string ServiceCreate = "Service.Create";
    public const string ServiceUpdate = "Service.Update";
    public const string AccommodationTypeCreate = "AccommodationType.Create";
    public const string AccommodationUnitCreate = "AccommodationUnit.Create";
}
