using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.Domain.Activities.PropertyEvents;

/// <summary>
/// A property event's information (Journey 8). Validated and normalized by <see cref="PropertyEvent"/>.
/// </summary>
/// <param name="Title">Display title, e.g. <c>Jazz on the Terrace</c>; required.</param>
/// <param name="Category">Broad grouping.</param>
/// <param name="Audience">Who it is for.</param>
/// <param name="LocalStart">Start in property-local time (its <see cref="DateTime.Kind"/> is ignored).</param>
/// <param name="LocalEnd">End in property-local time; after <paramref name="LocalStart"/>.</param>
/// <param name="Description">What happens; optional.</param>
/// <param name="LocationSpaceId">Where, a space in the same property (checked by the application layer); optional.</param>
/// <param name="Price">Price per person; <c>null</c> when free or not published.</param>
/// <param name="BookingRequired">Whether guests must book. Phase 1 cannot book, so the AI must tell guests how to book and never imply a reservation was made.</param>
public sealed record PropertyEventDetails(
    string Title,
    PropertyEventCategory Category,
    EventAudience Audience,
    DateTime LocalStart,
    DateTime LocalEnd,
    string? Description = null,
    SpaceId? LocationSpaceId = null,
    Money? Price = null,
    bool BookingRequired = false);
