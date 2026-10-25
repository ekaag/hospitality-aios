namespace HospitalityOS.SharedKernel.Results;

/// <summary>
/// One invalid input field.
/// </summary>
/// <param name="Field">Field name as the caller sent it, camelCase, e.g. <c>timeZone</c>.</param>
/// <param name="Code">Stable code, e.g. <c>property.time_zone_invalid</c>.</param>
/// <param name="Message">Human-readable description.</param>
public sealed record FieldError(string Field, string Code, string Message);
