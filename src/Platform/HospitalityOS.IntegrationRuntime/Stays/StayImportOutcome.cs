namespace HospitalityOS.IntegrationRuntime.Stays;

/// <summary>
/// What an import did with one PMS stay.
/// </summary>
public enum StayImportOutcome
{
    /// <summary>
    /// A new canonical stay was created and checked in.
    /// </summary>
    Created,

    /// <summary>
    /// The PMS stay was linked to an existing in-house stay in the same unit (its canonical ID is kept).
    /// </summary>
    Matched,

    /// <summary>
    /// The linked stay was brought up to date.
    /// </summary>
    Updated,

    /// <summary>
    /// The linked stay already matched the PMS.
    /// </summary>
    Unchanged,
}
