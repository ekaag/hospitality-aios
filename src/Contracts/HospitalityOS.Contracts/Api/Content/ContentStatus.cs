namespace HospitalityOS.Contracts.Api.Content;

/// <summary>
/// Where a content item is in its lifecycle. Only published content reaches guests and the concierge.
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
