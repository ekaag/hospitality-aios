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
    /// The caller changed a version it no longer has: someone else changed the record since it was read.
    /// </summary>
    public static Error VersionMismatch(long expected, long actual) =>
        Error.Conflict(VersionMismatchCode, $"The record is at version {actual}, not {expected}: reload it and apply the change again.");
}
