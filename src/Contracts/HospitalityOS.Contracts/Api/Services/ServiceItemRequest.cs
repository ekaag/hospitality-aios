namespace HospitalityOS.Contracts.Api.Services;

/// <summary>
/// Something guests can ask for as part of a service, e.g. a bath towel.
/// </summary>
/// <param name="Code">Stable machine code, unique within the service, e.g. <c>bath_towel</c>; required.</param>
/// <param name="Name">Display name, e.g. <c>Bath towel</c>; required.</param>
/// <param name="MaxQuantityPerRequest">Most a guest may ask for at once, 1 to 100; required.</param>
public sealed record ServiceItemRequest(
    string? Code = null,
    string? Name = null,
    int? MaxQuantityPerRequest = null);
