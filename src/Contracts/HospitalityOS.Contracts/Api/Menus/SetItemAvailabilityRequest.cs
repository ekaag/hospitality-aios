namespace HospitalityOS.Contracts.Api.Menus;

/// <summary>
/// <c>PUT /api/v1/menus/{id}/items/{itemId}/availability</c>: marks one item available or sold out, without resending
/// the menu.
/// </summary>
/// <param name="IsAvailable">Whether it can be ordered; required.</param>
public sealed record SetItemAvailabilityRequest(bool? IsAvailable = null);
