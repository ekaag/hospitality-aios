namespace HospitalityOS.Domain.Properties.ContentItems;

/// <summary>
/// What kind of descriptive text a <see cref="ContentItem"/> is. Rules belong in policies, not content.
/// </summary>
public enum ContentType
{
    /// <summary>
    /// General description of the property or a subject.
    /// </summary>
    Overview,

    /// <summary>
    /// A question and its answer.
    /// </summary>
    Faq,

    /// <summary>
    /// Getting to or around the property.
    /// </summary>
    Directions,

    /// <summary>
    /// Time-bound news, e.g. lobby renovation.
    /// </summary>
    Announcement,

    /// <summary>
    /// Anything else.
    /// </summary>
    Other,
}
