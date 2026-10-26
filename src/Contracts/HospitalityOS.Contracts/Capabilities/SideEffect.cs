namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// The most far-reaching effect a capability can have (spec §11 <c>sideEffects</c>).
/// </summary>
public enum SideEffect
{
    /// <summary>
    /// Reads only; invoking it changes nothing.
    /// </summary>
    None,

    /// <summary>
    /// Changes platform records and raises their events, e.g. creates a service request.
    /// </summary>
    PlatformState,

    /// <summary>
    /// Reaches people or systems outside the platform, e.g. sends a guest a message, and cannot be taken back.
    /// </summary>
    External,
}
