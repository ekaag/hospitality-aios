using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.Contracts.Capabilities.Activities;

/// <summary>
/// An upcoming or ongoing event.
/// </summary>
/// <param name="Source">The event record.</param>
/// <param name="Title">Title.</param>
/// <param name="Category">Kind.</param>
/// <param name="Audience">Who it is for.</param>
/// <param name="LocalStart">Starts (property-local).</param>
/// <param name="LocalEnd">Ends (property-local).</param>
/// <param name="Description">Description.</param>
/// <param name="LocationSpaceId">Where.</param>
/// <param name="Price">Price; absent when free or not listed.</param>
/// <param name="BookingRequired">Whether guests must book; explain how, never imply a booking was made.</param>
public sealed record EventEntry(
    SourceReference Source,
    string Title,
    PropertyEventCategory Category,
    EventAudience Audience,
    DateTime LocalStart,
    DateTime LocalEnd,
    string? Description,
    SpaceId? LocationSpaceId,
    Money? Price,
    bool BookingRequired);
