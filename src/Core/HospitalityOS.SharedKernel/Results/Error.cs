namespace HospitalityOS.SharedKernel.Results;

/// <summary>
/// An expected failure, returned in a <see cref="Result"/> rather than thrown.
/// </summary>
/// <param name="Code">Stable, machine-readable code, lower-case dotted, e.g. <c>humanaction.already_failed</c>.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Kind">The failure category.</param>
public record Error(string Code, string Message, ErrorKind Kind)
{
    /// <summary>
    /// Creates a <see cref="ErrorKind.NotFound"/> error.
    /// </summary>
    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    /// <summary>
    /// Creates a <see cref="ErrorKind.Conflict"/> error.
    /// </summary>
    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    /// <summary>
    /// Creates an <see cref="ErrorKind.InvalidState"/> error.
    /// </summary>
    public static Error InvalidState(string code, string message) => new(code, message, ErrorKind.InvalidState);

    /// <summary>
    /// Creates a <see cref="ErrorKind.Validation"/> error for a single field.
    /// </summary>
    public static ValidationError Validation(string field, string code, string message) =>
        ValidationError.From([new FieldError(field, code, message)]);
}
