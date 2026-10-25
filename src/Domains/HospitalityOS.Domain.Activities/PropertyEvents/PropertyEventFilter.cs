namespace HospitalityOS.Domain.Activities.PropertyEvents;

/// <summary>
/// Narrows event discovery (Journey 8: by time, category, and audience). Times are property-local.
/// </summary>
/// <param name="LocalFrom">Only events still running after this local time; <c>null</c> for no lower bound beyond "now".</param>
/// <param name="LocalTo">Only events starting before this local time, e.g. end of tonight; <c>null</c> for no upper bound.</param>
/// <param name="Category">Only this category; <c>null</c> for any.</param>
/// <param name="Audience">Only events for this audience or for <see cref="EventAudience.Everyone"/>; <c>null</c> for any.</param>
public sealed record PropertyEventFilter(
    DateTime? LocalFrom = null,
    DateTime? LocalTo = null,
    PropertyEventCategory? Category = null,
    EventAudience? Audience = null)
{
    /// <summary>
    /// No narrowing: every upcoming or ongoing event.
    /// </summary>
    public static PropertyEventFilter None { get; } = new();
}
