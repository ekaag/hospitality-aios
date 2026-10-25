using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Domain.Services.Services;

/// <summary>
/// A service's definition. Validated and normalized by <see cref="Service"/>.
/// </summary>
/// <param name="Code">Stable machine code, e.g. <c>housekeeping.amenities</c>; unique within the property.</param>
/// <param name="Name">Display name, e.g. <c>Housekeeping amenities</c>; required.</param>
/// <param name="Category">Which kind of team fulfils it.</param>
/// <param name="Description">What the service covers; optional.</param>
/// <param name="IsGuestRequestable">Whether guests may request it through the concierge.</param>
/// <param name="Items">Requestable items, in display order; empty for services requested by description only (e.g. repairs).</param>
/// <param name="TargetCompletion">Target time from request to completion, used for HumanAction SLAs; optional.</param>
public sealed record ServiceDetails(
    string Code,
    string Name,
    ServiceCategory Category,
    string? Description = null,
    bool IsGuestRequestable = true,
    ValueList<ServiceItem>? Items = null,
    TimeSpan? TargetCompletion = null);
