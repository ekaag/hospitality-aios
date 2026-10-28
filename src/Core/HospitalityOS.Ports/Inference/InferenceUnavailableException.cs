namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Thrown by an inference adapter when its provider cannot serve the call now (outage, timeout, overload, rate limit),
/// so the router may try the task's fallback. A request the provider rejects as invalid is a defect and must not use
/// this exception: falling back would only hide it.
/// </summary>
public sealed class InferenceUnavailableException : Exception
{
    /// <summary>
    /// Creates the exception with a default message.
    /// </summary>
    public InferenceUnavailableException()
    {
    }

    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">What went wrong, without request content (it may hold guest data).</param>
    public InferenceUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the exception wrapping the provider's failure.
    /// </summary>
    /// <param name="message">What went wrong, without request content (it may hold guest data).</param>
    /// <param name="innerException">The provider client's exception.</param>
    public InferenceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
