namespace HospitalityOS.Contracts.Api.FailedEvents;

/// <summary>
/// Where processing an event stopped.
/// </summary>
public enum FailedEventStage
{
    /// <summary>
    /// The event could not be published: no handler has seen it.
    /// </summary>
    Publish,

    /// <summary>
    /// A handler kept failing on it; other handlers may have handled it.
    /// </summary>
    Handle,
}
