namespace HospitalityOS.SharedKernel.Results;

/// <summary>
/// The outcome of an operation that produces a <typeparamref name="T"/> or fails in an expected way.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? value;

    internal Result(T? value, Error? error)
        : base(error)
    {
        this.value = value;
    }

    /// <summary>
    /// The value. Reading it from a failed result is a programming error.
    /// </summary>
    public T Value => IsSuccess
        ? value!  // Non-null whenever IsSuccess: only Success<T>(value) creates a successful Result<T>.
        : throw new InvalidOperationException($"A failed result has no value ({Error.Code}).");

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
