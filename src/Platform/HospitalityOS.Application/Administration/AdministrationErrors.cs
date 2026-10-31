using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Administration;

/// <summary>
/// Expected failures of the administration services.
/// </summary>
public static class AdministrationErrors
{
    /// <summary>
    /// Code of <see cref="VersionMismatch"/>.
    /// </summary>
    public const string VersionMismatchCode = "request.version_mismatch";

    /// <summary>
    /// The organization does not exist in the caller's tenant.
    /// </summary>
    public static Error OrganizationNotFound { get; } = Error.NotFound("organization.not_found", "There is no such organization.");

    /// <summary>
    /// The property does not exist in the caller's tenant.
    /// </summary>
    public static Error PropertyNotFound { get; } = Error.NotFound("property.not_found", "There is no such property.");

    /// <summary>
    /// The space does not exist in the caller's tenant.
    /// </summary>
    public static Error SpaceNotFound { get; } = Error.NotFound("space.not_found", "There is no such space.");

    /// <summary>
    /// The amenity does not exist in the caller's tenant.
    /// </summary>
    public static Error AmenityNotFound { get; } = Error.NotFound("amenity.not_found", "There is no such amenity.");

    /// <summary>
    /// The operating hours record does not exist in the caller's tenant.
    /// </summary>
    public static Error OperatingHoursNotFound { get; } = Error.NotFound("hours.not_found", "There are no such operating hours.");

    /// <summary>
    /// The subject already has operating hours: there is one record per subject, changed with an update.
    /// </summary>
    public static Error OperatingHoursAlreadySet { get; } =
        Error.Conflict("hours.already_set", "The subject already has operating hours: update that record instead.");

    /// <summary>
    /// The policy does not exist in the caller's tenant.
    /// </summary>
    public static Error PolicyNotFound { get; } = Error.NotFound("policy.not_found", "There is no such policy.");

    /// <summary>
    /// The content item does not exist in the caller's tenant.
    /// </summary>
    public static Error ContentItemNotFound { get; } = Error.NotFound("content.not_found", "There is no such content item.");

    /// <summary>
    /// The restaurant does not exist in the caller's tenant.
    /// </summary>
    public static Error RestaurantNotFound { get; } = Error.NotFound("restaurant.not_found", "There is no such restaurant.");

    /// <summary>
    /// The menu does not exist in the caller's tenant.
    /// </summary>
    public static Error MenuNotFound { get; } = Error.NotFound("menu.not_found", "There is no such menu.");

    /// <summary>
    /// The event does not exist in the caller's tenant.
    /// </summary>
    public static Error PropertyEventNotFound { get; } = Error.NotFound("property_event.not_found", "There is no such event.");

    /// <summary>
    /// The service does not exist in the caller's tenant.
    /// </summary>
    public static Error ServiceNotFound { get; } = Error.NotFound("service.not_found", "There is no such service.");

    /// <summary>
    /// The staff member does not exist in the caller's tenant.
    /// </summary>
    public static Error StaffMemberNotFound { get; } = Error.NotFound("staff_member.not_found", "There is no such staff member.");

    /// <summary>
    /// A worker may put only themselves on or off duty; managers and administrators may put anyone.
    /// </summary>
    public static Error DutyForbidden { get; } = Error.Forbidden("staff_member.duty_forbidden", "Only managers may put someone else on or off duty.");

    /// <summary>
    /// The room type does not exist in the caller's tenant.
    /// </summary>
    public static Error AccommodationTypeNotFound { get; } = Error.NotFound("accommodation_type.not_found", "There is no such room type.");

    /// <summary>
    /// The room does not exist in the caller's tenant.
    /// </summary>
    public static Error AccommodationUnitNotFound { get; } = Error.NotFound("accommodation_unit.not_found", "There is no such room.");

    /// <summary>
    /// The caller changed a version it no longer has: someone else changed the record since it was read.
    /// </summary>
    public static Error VersionMismatch(long expected, long actual) =>
        Error.Conflict(VersionMismatchCode, $"The record is at version {actual}, not {expected}: reload it and apply the change again.");
}
