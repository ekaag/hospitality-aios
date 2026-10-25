namespace HospitalityOS.Domain.Properties.ContentItems;

/// <summary>
/// Lifecycle of a <see cref="ContentItem"/>. Only published content is searchable by the concierge.
/// </summary>
public enum ContentStatus
{
    /// <summary>
    /// Being written.
    /// </summary>
    Draft,

    /// <summary>
    /// Visible to guests and the concierge.
    /// </summary>
    Published,

    /// <summary>
    /// Withdrawn; can be published again.
    /// </summary>
    Archived,
}
