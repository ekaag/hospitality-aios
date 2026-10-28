using HospitalityOS.Contracts.Capabilities.Activities;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.Contracts.Api.Events;

/// <summary>
/// <c>POST /api/v1/events</c>: schedules something happening at the property, e.g. live music (Journey 8). Guests are
/// offered it until it ends.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="PropertyId">The property; required.</param>
/// <param name="Title">Display title, e.g. <c>Jazz on the Terrace</c>; required.</param>
/// <param name="Category">The kind of event; required.</param>
/// <param name="Audience">Who it is for; everyone when omitted.</param>
/// <param name="LocalStart">Start in property-local time, written without an offset, e.g. <c>2026-11-01T19:00:00</c>;
/// required.</param>
/// <param name="LocalEnd">End in property-local time, after the start and at most 14 days later; required.</param>
/// <param name="Description">What happens.</param>
/// <param name="LocationSpaceId">Where: a space in the same property.</param>
/// <param name="Price">Price per person; omit when free or not published.</param>
/// <param name="BookingRequired">Whether guests must book; the concierge then explains how, never implying a booking was
/// made. Not required when omitted.</param>
public sealed record CreatePropertyEventRequest(
    PropertyId? PropertyId = null,
    string? Title = null,
    PropertyEventCategory? Category = null,
    EventAudience? Audience = null,
    DateTime? LocalStart = null,
    DateTime? LocalEnd = null,
    string? Description = null,
    SpaceId? LocationSpaceId = null,
    Money? Price = null,
    bool? BookingRequired = null);
