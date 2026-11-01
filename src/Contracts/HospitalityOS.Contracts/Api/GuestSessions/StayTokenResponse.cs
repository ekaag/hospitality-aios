namespace HospitalityOS.Contracts.Api.GuestSessions;

/// <summary>
/// A stay token: the guest's bearer token for the guest-facing endpoints, until it expires; the guest app then opens a
/// session again with the same link.
/// </summary>
/// <param name="AccessToken">The token.</param>
/// <param name="ExpiresAt">When it stops working.</param>
public sealed record StayTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
