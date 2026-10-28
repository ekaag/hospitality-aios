namespace HospitalityOS.Contracts.Api.Services;

/// <summary>
/// A requestable item, normalized (code lower-cased).
/// </summary>
/// <param name="Code">Machine code.</param>
/// <param name="Name">Display name.</param>
/// <param name="MaxQuantityPerRequest">Most a guest may ask for at once.</param>
public sealed record ServiceItemResponse(string Code, string Name, int MaxQuantityPerRequest);
