using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Services;

/// <summary>
/// <c>PUT /api/v1/services/{id}</c>: replaces a service's definition; omitting an optional field clears it.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="Code">Stable machine code, unique within the property; required.</param>
/// <param name="Name">Display name; required.</param>
/// <param name="Category">Which kind of team fulfils it; required.</param>
/// <param name="Description">What it covers.</param>
/// <param name="IsGuestRequestable">Whether guests may request it through the concierge; yes when omitted.</param>
/// <param name="Items">Requestable items, in display order.</param>
/// <param name="TargetCompletion">Target time from request to completion, at most 7 days.</param>
public sealed record UpdateServiceRequest(
    string? Code = null,
    string? Name = null,
    ServiceCategory? Category = null,
    string? Description = null,
    bool? IsGuestRequestable = null,
    ValueList<ServiceItemRequest>? Items = null,
    TimeSpan? TargetCompletion = null);
