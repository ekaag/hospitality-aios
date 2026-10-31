using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.HumanActions;

/// <summary>
/// Expected failures of the human action capabilities.
/// </summary>
internal static class HumanActionErrors
{
    public static Error NotFound { get; } = Error.NotFound("human_action.not_found", "There is no such human action.");

    public static Error NotStaffMember { get; } =
        Error.Forbidden("human_action.not_staff_member", "Only a staff member known by their staff ID can do this to a human action.");

    public static Error ServiceRequestNotFound { get; } =
        Error.Validation("serviceRequestId", "human_action.service_request_not_found", "The property has no such service request.");

    public static Error StatusNotSettable { get; } =
        Error.Validation("status", "human_action.status_not_settable", "The status can be set to Accepted, InProgress, or Failed.");

    public static Error Invalid(string field, string value) =>
        Error.Validation(field, $"human_action.{field}_invalid", $"'{value}' is not a known {field}.");
}
