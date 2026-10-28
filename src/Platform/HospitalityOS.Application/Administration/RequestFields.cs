using HospitalityOS.Contracts;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Administration;

/// <summary>
/// Field errors the request causes before the domain sees it (a required choice left out, a referenced record that is
/// not in the property), reported together with the domain's own so the caller sees every invalid field at once.
/// </summary>
internal sealed class RequestFields
{
    private readonly List<FieldError> errors = [];

    public bool HasErrors => errors.Count > 0;

    public void Add(string field, string code, string message) => errors.Add(new FieldError(field, code, message));

    /// <summary>
    /// The domain member mirroring a required contract choice. When it is missing, records <paramref name="code"/> and
    /// returns a placeholder the domain accepts: the command is rejected anyway.
    /// </summary>
    public TDomain Required<TDomain>(Enum? value, string field, string code)
        where TDomain : struct, Enum
    {
        if (value is not null)
        {
            return EnumMirror.To<TDomain>(value);
        }

        Add(field, code, $"{field} is required.");

        return default;
    }

    /// <summary>
    /// The new aggregate the domain created, or when the request had errors, a <see cref="ValidationError"/> listing the
    /// domain's field errors followed by the request's. Safe because a rejected new aggregate is never added.
    /// </summary>
    public Result<T> Merge<T>(Result<T> created)
    {
        ArgumentNullException.ThrowIfNull(created);

        return errors.Count == 0 ? created : ValidationError.From([.. DomainFields(created), .. errors]);
    }

    /// <summary>
    /// Runs a change to a loaded aggregate only when the request had no errors; otherwise reports the request's. The
    /// domain must not run then: given a placeholder it could change the aggregate, and the rejection's audit record is
    /// committed in the same unit of work.
    /// </summary>
    public Result Apply(Func<Result> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return errors.Count == 0 ? change() : ValidationError.From(errors);
    }

    private static IReadOnlyList<FieldError> DomainFields(Result validated) =>
        validated is { IsFailure: true, Error: ValidationError validation } ? validation.Fields : [];
}
