using HospitalityOS.SharedKernel.Locations;

namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// What was requested.
/// </summary>
/// <param name="Location">Where it is needed.</param>
/// <param name="ItemCode">The item, if any.</param>
/// <param name="Quantity">How many.</param>
/// <param name="Description">Free-text description, if any.</param>
public sealed record ServiceRequestDetailsData(
    ActionLocation Location,
    string? ItemCode,
    int Quantity,
    string? Description);
