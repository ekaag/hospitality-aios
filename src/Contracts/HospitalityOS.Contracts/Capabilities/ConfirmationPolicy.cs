namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// Whether a capability runs only after the person it acts for explicitly confirms it (spec §11
/// <c>confirmationPolicy</c>). Confirmation is asked for only where policy requires it (spec §35).
/// </summary>
public enum ConfirmationPolicy
{
    /// <summary>
    /// Runs as soon as it is invoked.
    /// </summary>
    NotRequired,

    /// <summary>
    /// Runs only once the invocation carries an explicit confirmation; otherwise it is refused and the caller must ask.
    /// </summary>
    Required,
}
