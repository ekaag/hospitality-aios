namespace HospitalityOS.Contracts.Capabilities.Activities;

/// <summary>
/// Who an event is for.
/// </summary>
public enum EventAudience
{
    /// <summary>
    /// Everyone.
    /// </summary>
    Everyone,

    /// <summary>
    /// Adults only.
    /// </summary>
    AdultsOnly,

    /// <summary>
    /// Families.
    /// </summary>
    Families,

    /// <summary>
    /// Children.
    /// </summary>
    Children,
}
