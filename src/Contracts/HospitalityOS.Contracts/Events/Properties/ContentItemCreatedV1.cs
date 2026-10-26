using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>ContentItemCreated</c>: a content item was created.
/// </summary>
/// <param name="ContentItemId">The content item.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Details">The full, normalized details after the change.</param>
/// <param name="Status">The status after the change (domain enum name).</param>
public sealed record ContentItemCreatedV1(
    Guid ContentItemId,
    PropertyId PropertyId,
    ContentItemDetailsData Details,
    string Status) : IEventPayload;
