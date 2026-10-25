namespace HospitalityOS.Domain.Services.Services;

/// <summary>
/// Something a guest can ask for as part of a service, e.g. a bath towel. Extraction (I1) maps guest
/// wording to an item code and checks the quantity limit.
/// </summary>
/// <param name="Code">Stable machine code, e.g. <c>bath_towel</c>; unique within the service.</param>
/// <param name="Name">Display name, e.g. <c>Bath towel</c>.</param>
/// <param name="MaxQuantityPerRequest">Most a guest may ask for at once.</param>
public sealed record ServiceItem(string Code, string Name, int MaxQuantityPerRequest);
