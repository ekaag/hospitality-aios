using HospitalityOS.Contracts.Events.Values;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>ContentItemUpdated</c>: a content item changed, was published, or was archived.
/// </summary>
/// <param name="ContentItemId">The content item.</param>
/// <param name="Details">The full, normalized details after the change.</param>
/// <param name="Status">The status after the change (domain enum name).</param>
public sealed record ContentItemUpdatedV1(
    Guid ContentItemId,
    ContentItemDetailsData Details,
    string Status) : IEventPayload;
