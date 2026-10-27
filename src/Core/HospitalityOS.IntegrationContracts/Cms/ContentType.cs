namespace HospitalityOS.IntegrationContracts.Cms;

/// <summary>
/// The kind of a <see cref="CmsContent"/>, named as the canonical content type.
/// </summary>
public enum ContentType
{
    /// <summary>
    /// General description of the property or a facility.
    /// </summary>
    Overview,

    /// <summary>
    /// A question and its answer.
    /// </summary>
    Faq,

    /// <summary>
    /// How to get somewhere.
    /// </summary>
    Directions,

    /// <summary>
    /// A notice for guests.
    /// </summary>
    Announcement,

    /// <summary>
    /// Anything else.
    /// </summary>
    Other,
}
