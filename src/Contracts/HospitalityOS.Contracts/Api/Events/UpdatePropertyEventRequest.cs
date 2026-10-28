using HospitalityOS.Contracts.Capabilities.Activities;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.Contracts.Api.Events;

/// <summary>
/// <c>PUT /api/v1/events/{id}</c>: replaces a scheduled event's information; a cancelled event cannot change.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="Title">Display title; required.</param>
/// <param name="Category">The kind of event; required.</param>
/// <param name="Audience">Who it is for; everyone when omitted.</param>
/// <param name="LocalStart">Start in property-local time, written without an offset; required.</param>
/// <param name="LocalEnd">End in property-local time; required.</param>
/// <param name="Description">What happens.</param>
/// <param name="LocationSpaceId">Where: a space in the same property.</param>
/// <param name="Price">Price per person.</param>
/// <param name="BookingRequired">Whether guests must book; not required when omitted.</param>
public sealed record UpdatePropertyEventRequest(
    string? Title = null,
    PropertyEventCategory? Category = null,
    EventAudience? Audience = null,
    DateTime? LocalStart = null,
    DateTime? LocalEnd = null,
    string? Description = null,
    SpaceId? LocationSpaceId = null,
    Money? Price = null,
    bool? BookingRequired = null);
