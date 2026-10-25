namespace HospitalityOS.Domain.Activities.PropertyEvents;

/// <summary>
/// Who a <see cref="PropertyEvent"/> is for.
/// </summary>
public enum EventAudience
{
    /// <summary>
    /// Open to all guests; matches any audience filter.
    /// </summary>
    Everyone,

    /// <summary>
    /// Adults only, e.g. a wine tasting.
    /// </summary>
    AdultsOnly,

    /// <summary>
    /// Families together.
    /// </summary>
    Families,

    /// <summary>
    /// Children, e.g. kids' club.
    /// </summary>
    Children,
}
