using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Domain.Properties.Shared;

/// <summary>
/// Trimming and length rules for text fields, shared by this module's aggregates. Each method records a
/// <see cref="FieldError"/> instead of failing, so callers can report every invalid field at once.
/// </summary>
internal static class TextRules
{
    public const int MaxTextLength = 200;
    public const int MaxLongTextLength = 2000;

    /// <summary>
    /// Trims a required value; records <c>{codePrefix}_required</c> or <c>{codePrefix}_too_long</c>.
    /// </summary>
    public static string Required(string? value, string field, string codePrefix, List<FieldError> errors, int maxLength = MaxTextLength)
    {
        string trimmed = value?.Trim() ?? "";

        if (trimmed.Length == 0)
        {
            errors.Add(new FieldError(field, codePrefix + "_required", $"{field} is required."));
        }
        else if (trimmed.Length > maxLength)
        {
            errors.Add(new FieldError(field, codePrefix + "_too_long", $"{field} must be at most {maxLength} characters."));
        }

        return trimmed;
    }

    /// <summary>
    /// Trims an optional value to <c>null</c> when blank; records <c>{codePrefix}_too_long</c>.
    /// </summary>
    public static string? Optional(string? value, string field, string codePrefix, List<FieldError> errors, int maxLength = MaxTextLength)
    {
        string? trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        if (trimmed is not null && trimmed.Length > maxLength)
        {
            errors.Add(new FieldError(field, codePrefix + "_too_long", $"{field} must be at most {maxLength} characters."));
        }

        return trimmed;
    }
}
