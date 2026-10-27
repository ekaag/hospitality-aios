using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.Reading;

/// <summary>
/// Expected failures of the read capabilities. A not-found answer means there is no authoritative source: the agent
/// says it does not know rather than guessing (P4).
/// </summary>
internal static class ReadErrors
{
    public static Error PropertyNotFound { get; } = Error.NotFound("property.not_found", "There is no such property.");

    public static Error RestaurantNotFound { get; } = Error.NotFound("restaurant.not_found", "There is no such restaurant.");

    public static Error HoursNotFound { get; } =
        Error.NotFound("hours.not_found", "No opening hours are recorded for this; say they are not known rather than guessing.");

    public static Error SubjectIdRequired { get; } =
        Error.Validation("subjectId", "hours.subject_id_required", "Give the ID of the amenity, restaurant, space, or service.");

    public static Error OutOfRange(string field, int min, int max) =>
        Error.Validation(field, "capability.out_of_range", $"{field} must be from {min} to {max}.");

    public static Error Required(string field) =>
        Error.Validation(field, "capability.required", $"{field} is required.");
}
