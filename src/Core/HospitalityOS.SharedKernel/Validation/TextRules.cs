using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.SharedKernel.Validation;

/// <summary>
/// Trimming and length rules for text fields, shared by all domain modules. Each method records a
/// <see cref="FieldError"/> instead of failing, so callers can report every invalid field at once.
/// </summary>
public static class TextRules
{
    /// <summary>
    /// Default maximum for short text such as names and titles.
    /// </summary>
    public const int MaxTextLength = 200;

    /// <summary>
    /// Default maximum for descriptions and directions.
    /// </summary>
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
    /// Maximum length of a machine code (see <see cref="Code"/>).
    /// </summary>
    public const int MaxCodeLength = 64;

    /// <summary>
    /// Normalizes a required machine code to lower case — letters and digits in segments joined by
    /// <c>.</c>, <c>_</c> or <c>-</c>, e.g. <c>housekeeping.bath_towel</c>; records <c>{codePrefix}_required</c>,
    /// <c>{codePrefix}_too_long</c> or <c>{codePrefix}_invalid</c>.
    /// </summary>
    public static string Code(string? value, string field, string codePrefix, List<FieldError> errors)
    {
        int errorCount = errors.Count;
        string code = Required(value, field, codePrefix, errors, MaxCodeLength).ToLowerInvariant();

        if (errors.Count == errorCount && !IsCode(code))
        {
            errors.Add(new FieldError(field, codePrefix + "_invalid", $"{field} may contain only letters, digits, and single '.', '_' or '-' separators."));
        }

        return code;
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

    private static bool IsCode(string code)
    {
        bool previousWasSeparator = true;

        foreach (char character in code)
        {
            bool isSeparator = character is '.' or '_' or '-';

            if (isSeparator ? previousWasSeparator : !(char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character)))
            {
                return false;
            }

            previousWasSeparator = isSeparator;
        }

        return !previousWasSeparator;
    }
}
