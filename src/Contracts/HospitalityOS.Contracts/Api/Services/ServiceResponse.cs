using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Services;

/// <summary>
/// A service, normalized (trimmed, codes lower-cased).
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Code">Machine code, unique within the property.</param>
/// <param name="Name">Display name.</param>
/// <param name="Category">Which kind of team fulfils it.</param>
/// <param name="Description">What it covers.</param>
/// <param name="IsGuestRequestable">Whether guests may request it through the concierge.</param>
/// <param name="Items">Requestable items, in display order.</param>
/// <param name="TargetCompletion">Target time from request to completion.</param>
/// <param name="Version">Increases with every change; send it back in <c>If-Match</c> to update safely.</param>
public sealed record ServiceResponse(
    Guid Id,
    PropertyId PropertyId,
    string Code,
    string Name,
    ServiceCategory Category,
    string? Description,
    bool IsGuestRequestable,
    ValueList<ServiceItemResponse> Items,
    TimeSpan? TargetCompletion,
    long Version);
