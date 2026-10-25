namespace HospitalityOS.SharedKernel.Results;

/// <summary>
/// The outcome of an operation that can fail in an expected way (coding-standards.md §3).
/// Exceptions remain for programming errors and infrastructure faults.
/// </summary>
public class Result
{
    private static readonly Result success = new(null);

    private readonly Error? error;

    private protected Result(Error? error)
    {
        this.error = error;
    }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool IsSuccess => error is null;

    /// <summary>
    /// Whether the operation failed.
    /// </summary>
    public bool IsFailure => error is not null;

    /// <summary>
    /// The failure. Reading it from a successful result is a programming error.
    /// </summary>
    public Error Error => error ?? throw new InvalidOperationException("A successful result has no error.");

    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>
    /// A successful result with no value.
    /// </summary>
    public static Result Success() => success;

    /// <summary>
    /// A successful result carrying <paramref name="value"/>.
    /// </summary>
    public static Result<T> Success<T>(T value) => new(value, null);

    /// <summary>
    /// A failed result.
    /// </summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result(error);
    }

    /// <summary>
    /// A failed result for an operation that would have produced a <typeparamref name="T"/>.
    /// </summary>
    public static Result<T> Failure<T>(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<T>(default, error);
    }
}
