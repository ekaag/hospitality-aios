using HospitalityOS.Contracts.Capabilities.Activities;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.Contracts.Api.Events;

/// <summary>
/// An event, normalized (trimmed).
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Title">Display title.</param>
/// <param name="Category">The kind of event.</param>
/// <param name="Audience">Who it is for.</param>
/// <param name="LocalStart">Start, property-local.</param>
/// <param name="LocalEnd">End, property-local.</param>
/// <param name="Description">What happens.</param>
/// <param name="LocationSpaceId">Where, if recorded.</param>
/// <param name="Price">Price per person, if published.</param>
/// <param name="BookingRequired">Whether guests must book.</param>
/// <param name="Status">Whether it is going ahead.</param>
/// <param name="Version">Increases with every change; send it back in <c>If-Match</c> to update safely.</param>
public sealed record PropertyEventResponse(
    Guid Id,
    PropertyId PropertyId,
    string Title,
    PropertyEventCategory Category,
    EventAudience Audience,
    DateTime LocalStart,
    DateTime LocalEnd,
    string? Description,
    SpaceId? LocationSpaceId,
    Money? Price,
    bool BookingRequired,
    PropertyEventStatus Status,
    long Version);
