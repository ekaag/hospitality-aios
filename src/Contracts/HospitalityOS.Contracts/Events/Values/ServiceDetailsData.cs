using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// A requestable service's information.
/// </summary>
/// <param name="Code">Machine code, unique per property, e.g. <c>housekeeping.amenities</c>.</param>
/// <param name="Name">Name.</param>
/// <param name="Category">Housekeeping, maintenance, ... (domain enum name).</param>
/// <param name="Description">Description.</param>
/// <param name="IsGuestRequestable">Whether guests may request it.</param>
/// <param name="Items">Requestable items.</param>
/// <param name="TargetCompletion">Internal completion target.</param>
public sealed record ServiceDetailsData(
    string Code,
    string Name,
    string Category,
    string? Description,
    bool IsGuestRequestable,
    ValueList<ServiceItemData> Items,
    TimeSpan? TargetCompletion);
