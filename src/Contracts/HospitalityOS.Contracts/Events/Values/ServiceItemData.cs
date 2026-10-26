namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// An item a service can deliver.
/// </summary>
/// <param name="Code">Machine code, e.g. <c>bath_towel</c>.</param>
/// <param name="Name">Name.</param>
/// <param name="MaxQuantityPerRequest">Most a guest may ask for at once.</param>
public sealed record ServiceItemData(
    string Code,
    string Name,
    int MaxQuantityPerRequest);
