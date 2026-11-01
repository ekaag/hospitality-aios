namespace HospitalityOS.Contracts.Api.GuestSessions;

/// <summary>
/// Opens a guest session from a stay's check-in link.
/// </summary>
/// <param name="Code">The code the link carries.</param>
public sealed record OpenGuestSessionRequest(string? Code = null);
