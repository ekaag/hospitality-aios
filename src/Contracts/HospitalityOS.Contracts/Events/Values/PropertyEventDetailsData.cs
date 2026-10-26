using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// An event or activity's information.
/// </summary>
/// <param name="Title">Title.</param>
/// <param name="Category">Entertainment, fitness, ... (domain enum name).</param>
/// <param name="Audience">Who it is for (domain enum name).</param>
/// <param name="LocalStart">Start in property-local time.</param>
/// <param name="LocalEnd">End in property-local time.</param>
/// <param name="Description">Description.</param>
/// <param name="LocationSpaceId">Where it is.</param>
/// <param name="Price">Price, if any.</param>
/// <param name="BookingRequired">Whether guests must book.</param>
public sealed record PropertyEventDetailsData(
    string Title,
    string Category,
    string Audience,
    DateTime LocalStart,
    DateTime LocalEnd,
    string? Description,
    SpaceId? LocationSpaceId,
    Money? Price,
    bool BookingRequired);
