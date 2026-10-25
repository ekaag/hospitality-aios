namespace HospitalityOS.Domain.Properties.Policies;

/// <summary>
/// What a <see cref="Policy"/> governs, so the concierge can find the policy that answers a question.
/// </summary>
public enum PolicyType
{
    /// <summary>
    /// Minimum ages and supervision, e.g. "under-16s must be accompanied in the gym".
    /// </summary>
    AgeRestriction,

    /// <summary>
    /// Children and extra beds.
    /// </summary>
    Children,

    /// <summary>
    /// Pets and service animals.
    /// </summary>
    Pets,

    /// <summary>
    /// Smoking and vaping.
    /// </summary>
    Smoking,

    /// <summary>
    /// Check-in and check-out times and rules.
    /// </summary>
    CheckInCheckOut,

    /// <summary>
    /// Cancellation and no-show terms.
    /// </summary>
    Cancellation,

    /// <summary>
    /// Deposits, payment methods.
    /// </summary>
    Payment,

    /// <summary>
    /// Dress codes.
    /// </summary>
    DressCode,

    /// <summary>
    /// Safety and conduct, e.g. pool rules.
    /// </summary>
    Safety,

    /// <summary>
    /// Quiet hours and noise.
    /// </summary>
    Noise,

    /// <summary>
    /// Anything else.
    /// </summary>
    Other,
}
