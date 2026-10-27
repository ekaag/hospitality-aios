namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// The topic of a policy.
/// </summary>
public enum PolicyType
{
    /// <summary>
    /// Minimum ages.
    /// </summary>
    AgeRestriction,

    /// <summary>
    /// Children and supervision.
    /// </summary>
    Children,

    /// <summary>
    /// Pets.
    /// </summary>
    Pets,

    /// <summary>
    /// Smoking.
    /// </summary>
    Smoking,

    /// <summary>
    /// Check-in and check-out.
    /// </summary>
    CheckInCheckOut,

    /// <summary>
    /// Cancellation.
    /// </summary>
    Cancellation,

    /// <summary>
    /// Payment.
    /// </summary>
    Payment,

    /// <summary>
    /// Dress code.
    /// </summary>
    DressCode,

    /// <summary>
    /// Safety.
    /// </summary>
    Safety,

    /// <summary>
    /// Noise and quiet hours.
    /// </summary>
    Noise,

    /// <summary>
    /// Anything else.
    /// </summary>
    Other,
}
