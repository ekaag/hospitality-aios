using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Eventing.Handling;

/// <summary>
/// A handler's changes could not be committed; thrown so the transport redelivers the event.
/// </summary>
public sealed class EventHandlingException : Exception
{
    public EventHandlingException()
    {
    }

    public EventHandlingException(string message)
        : base(message)
    {
    }

    public EventHandlingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal EventHandlingException(string handler, Guid eventId, Error error)
        : base($"{handler} could not commit its handling of {eventId}: {error.Code} ({error.Message})")
    {
        Error = error;
    }

    /// <summary>
    /// Why the commit failed, when it was an expected failure.
    /// </summary>
    public Error? Error { get; }
}
