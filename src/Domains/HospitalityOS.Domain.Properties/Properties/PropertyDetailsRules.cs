using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Domain.Properties.Properties;

/// <summary>
/// Validates and normalizes <see cref="PropertyDetails"/>, reporting every invalid field at once.
/// </summary>
internal static class PropertyDetailsRules
{
    public const int MaxPhoneLength = 32;

    public static Result<PropertyDetails> Validate(PropertyDetails details)
    {
        List<FieldError> errors = [];

        string name = TextRules.Required(details.Name, "name", "property.name", errors);
        string timeZoneId = ValidateTimeZone(details.TimeZoneId, errors);
        PostalAddress? address = ValidateAddress(details.Address, errors);
        string? phone = TextRules.Optional(details.ContactPhone, "contactPhone", "property.contact_phone", errors, MaxPhoneLength);
        string? email = ValidateEmail(details.ContactEmail, errors);

        if (errors.Count > 0)
        {
            return ValidationError.From(errors);
        }

        // address is non-null here: ValidateAddress adds an error whenever it returns null.
        return new PropertyDetails(name, timeZoneId, address!, phone, email);
    }

    private static string ValidateTimeZone(string? value, List<FieldError> errors)
    {
        string timeZoneId = TextRules.Required(value, "timeZone", "property.time_zone", errors);

        // Require an IANA ID so the value means the same on every OS and in every API client.
        bool isIana = timeZoneId.Length > 0
            && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out TimeZoneInfo? timeZone)
            && timeZone.HasIanaId;

        if (timeZoneId.Length > 0 && !isIana)
        {
            errors.Add(new FieldError("timeZone", "property.time_zone_invalid", $"'{timeZoneId}' is not a known IANA time zone, e.g. 'America/New_York'."));
        }

        return timeZoneId;
    }

    private static PostalAddress? ValidateAddress(PostalAddress? address, List<FieldError> errors)
    {
        if (address is null)
        {
            errors.Add(new FieldError("address", "property.address_required", "Address is required."));
            return null;
        }

        string line1 = TextRules.Required(address.Line1, "address.line1", "property.address_line1", errors);
        string? line2 = TextRules.Optional(address.Line2, "address.line2", "property.address_line2", errors);
        string city = TextRules.Required(address.City, "address.city", "property.address_city", errors);
        string? region = TextRules.Optional(address.Region, "address.region", "property.address_region", errors);
        string? postalCode = TextRules.Optional(address.PostalCode, "address.postalCode", "property.address_postal_code", errors);
        string countryCode = address.CountryCode?.Trim().ToUpperInvariant() ?? "";

        if (countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetterUpper))
        {
            errors.Add(new FieldError("address.countryCode", "property.address_country_code_invalid", "Country code must be a two-letter ISO 3166-1 code, e.g. 'US'."));
        }

        return new PostalAddress(line1, line2, city, region, postalCode, countryCode);
    }

    private static string? ValidateEmail(string? value, List<FieldError> errors)
    {
        string? email = TextRules.Optional(value, "contactEmail", "property.contact_email", errors);

        // Deliberately loose: one '@' with text on both sides. Deliverability is not a domain rule.
        int at = email?.IndexOf('@', StringComparison.Ordinal) ?? -1;

        if (email is not null && (at <= 0 || at == email.Length - 1 || email.IndexOf('@', at + 1) >= 0 || email.Any(char.IsWhiteSpace)))
        {
            errors.Add(new FieldError("contactEmail", "property.contact_email_invalid", "Contact email is not a valid email address."));
        }

        return email;
    }
}
