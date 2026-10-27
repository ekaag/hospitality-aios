using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Validation;

namespace HospitalityOS.Domain.Integrations.Shared;

/// <summary>
/// Name rules shared by the module's aggregates. Each method records a <see cref="FieldError"/> instead of failing.
/// </summary>
internal static class IntegrationRules
{
    /// <summary>
    /// A provider's registered name, e.g. <c>MockPms</c>: a letter, then letters and digits. Kept exactly, so it matches
    /// the provider registry's spelling.
    /// </summary>
    public static string ProviderName(string? value, List<FieldError> errors) =>
        Identifier(value, "providerName", "integration.provider", IntegrationConfiguration.MaxProviderNameLength, errors);

    /// <summary>
    /// A canonical type name, e.g. <c>StayContext</c>: an upper-case letter, then letters and digits.
    /// </summary>
    public static string TypeName(string? value, string field, string codePrefix, int maxLength, List<FieldError> errors)
    {
        int errorCount = errors.Count;
        string name = Identifier(value, field, codePrefix, maxLength, errors);

        if (errors.Count == errorCount && !char.IsAsciiLetterUpper(name[0]))
        {
            errors.Add(new FieldError(field, codePrefix + "_invalid", $"{field} must start with an upper-case letter."));
        }

        return name;
    }

    /// <summary>
    /// A setting name, e.g. <c>hotelCode</c> or <c>api.baseUrl</c>: letters and digits in segments joined by
    /// <c>.</c>, <c>_</c> or <c>-</c>; case is kept.
    /// </summary>
    public static string SettingName(string? value, string field, List<FieldError> errors)
    {
        int errorCount = errors.Count;
        string name = TextRules.Required(value, field, "integration.setting_name", errors, IntegrationConfiguration.MaxSettingNameLength);

        if (errors.Count == errorCount && !TextRules.IsCode(name.ToLowerInvariant()))
        {
            errors.Add(new FieldError(field, "integration.setting_name_invalid", $"{field} may contain only letters, digits, and single '.', '_' or '-' separators."));
        }

        return name;
    }

    private static string Identifier(string? value, string field, string codePrefix, int maxLength, List<FieldError> errors)
    {
        int errorCount = errors.Count;
        string name = TextRules.Required(value, field, codePrefix, errors, maxLength);

        if (errors.Count == errorCount && !(char.IsAsciiLetter(name[0]) && name.All(char.IsAsciiLetterOrDigit)))
        {
            errors.Add(new FieldError(field, codePrefix + "_invalid", $"{field} must be a letter followed by letters and digits."));
        }

        return name;
    }
}
