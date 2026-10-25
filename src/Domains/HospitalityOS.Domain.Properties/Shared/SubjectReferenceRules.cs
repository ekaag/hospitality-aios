using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Domain.Properties.Shared;

/// <summary>
/// Validates a <see cref="SubjectReference"/>, recording field errors under <c>{field}.kind</c> and <c>{field}.id</c>.
/// </summary>
internal static class SubjectReferenceRules
{
    public static void Validate(SubjectReference subject, string field, string codePrefix, List<FieldError> errors)
    {
        if (!Enum.IsDefined(subject.Kind))
        {
            errors.Add(new FieldError(field + ".kind", codePrefix + "_kind_invalid", $"'{subject.Kind}' is not a known subject kind."));
        }

        if (subject.Id == Guid.Empty)
        {
            errors.Add(new FieldError(field + ".id", codePrefix + "_id_required", $"{field}.id is required."));
        }
    }
}
