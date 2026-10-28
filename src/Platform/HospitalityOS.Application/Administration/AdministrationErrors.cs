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
    /// The caller changed a version it no longer has: someone else changed the record since it was read.
    /// </summary>
    public static Error VersionMismatch(long expected, long actual) =>
        Error.Conflict(VersionMismatchCode, $"The record is at version {actual}, not {expected}: reload it and apply the change again.");
}
