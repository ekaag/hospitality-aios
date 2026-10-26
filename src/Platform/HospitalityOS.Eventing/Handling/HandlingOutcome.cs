namespace HospitalityOS.Eventing.Handling;

/// <summary>
/// What processing one delivery did; duplicates are observable but harmless (Journey 19).
/// </summary>
internal enum HandlingOutcome
{
    /// <summary>
    /// The handler ran and its changes were committed.
    /// </summary>
    Handled,

    /// <summary>
    /// The handler had already processed the event (redelivery, replay, or a lost race); nothing changed.
    /// </summary>
    Duplicate,

    /// <summary>
    /// The event is not the type this handler handles.
    /// </summary>
    NotHandled,
}
