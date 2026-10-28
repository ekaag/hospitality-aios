using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Services;

/// <summary>
/// <c>POST /api/v1/services</c>: defines something the property does for guests, e.g. delivering towels or fixing the
/// air conditioning. Guest service requests name a service and, optionally, one of its items.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="PropertyId">The property; required.</param>
/// <param name="Code">Stable machine code, unique within the property, e.g. <c>housekeeping.amenities</c>; required.</param>
/// <param name="Name">Display name, e.g. <c>Housekeeping amenities</c>; required.</param>
/// <param name="Category">Which kind of team fulfils it; required.</param>
/// <param name="Description">What it covers.</param>
/// <param name="IsGuestRequestable">Whether guests may request it through the concierge; yes when omitted.</param>
/// <param name="Items">Requestable items, in display order; omit for services requested by description only (repairs).</param>
/// <param name="TargetCompletion">Target time from request to completion, e.g. <c>00:30:00</c>, at most 7 days; used for
/// staff deadlines, never quoted to guests.</param>
public sealed record CreateServiceRequest(
    PropertyId? PropertyId = null,
    string? Code = null,
    string? Name = null,
    ServiceCategory? Category = null,
    string? Description = null,
    bool? IsGuestRequestable = null,
    ValueList<ServiceItemRequest>? Items = null,
    TimeSpan? TargetCompletion = null);
