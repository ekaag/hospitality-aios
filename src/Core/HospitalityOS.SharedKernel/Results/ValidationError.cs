namespace HospitalityOS.SharedKernel.Results;

/// <summary>
/// A <see cref="ErrorKind.Validation"/> error listing every invalid field, so callers can report them all at once.
/// </summary>
public sealed record ValidationError : Error
{
    /// <summary>
    /// Code shared by all validation errors; per-field detail is in <see cref="Fields"/>.
    /// </summary>
    public const string ValidationFailedCode = "validation.failed";

    private ValidationError(IReadOnlyList<FieldError> fields)
        : base(ValidationFailedCode, BuildMessage(fields), ErrorKind.Validation)
    {
        Fields = fields;
    }

    /// <summary>
    /// The invalid fields, in the order reported. Never empty.
    /// </summary>
    public IReadOnlyList<FieldError> Fields { get; }

    /// <summary>
    /// Creates a validation error from one or more field errors.
    /// </summary>
    /// <param name="fields">The field errors; must not be empty.</param>
    /// <returns>The validation error.</returns>
    public static ValidationError From(IEnumerable<FieldError> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        FieldError[] copy = [.. fields];

        return copy.Length == 0
            ? throw new ArgumentException("A validation error needs at least one field error.", nameof(fields))
            : new ValidationError(copy);
    }

    private static string BuildMessage(IReadOnlyList<FieldError> fields) =>
        fields.Count == 1 ? fields[0].Message : $"{fields.Count} fields are invalid.";
}
