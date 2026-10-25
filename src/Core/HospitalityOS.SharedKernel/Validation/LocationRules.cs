using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.SharedKernel.Validation;

/// <summary>
/// Validates and normalizes an <see cref="ActionLocation"/>, recording field errors instead of failing.
/// </summary>
public static class LocationRules
{
    /// <summary>
    /// Maximum length of <see cref="ActionLocation.Label"/>.
    /// </summary>
    public const int MaxLabelLength = 100;

    /// <summary>
    /// Records <c>{codePrefix}_required</c> when missing, or a label error under <c>{field}.label</c>.
    /// </summary>
    /// <returns>The normalized location, or <c>null</c> when it was missing.</returns>
    public static ActionLocation? Validate(ActionLocation? location, string field, string codePrefix, List<FieldError> errors)
    {
        if (location is null)
        {
            errors.Add(new FieldError(field, codePrefix + "_required", $"{field} is required."));
            return null;
        }

        string label = TextRules.Required(location.Label, field + ".label", codePrefix + "_label", errors, MaxLabelLength);

        return location with { Label = label };
    }
}
