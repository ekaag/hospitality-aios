using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Stays;

/// <summary>
/// A stay's new check-in link: shown this once (only its code's hash is kept), to give the guest as a link or a QR code.
/// It works on any device until the stay checks out or the link is replaced.
/// </summary>
/// <param name="StayId">The stay.</param>
/// <param name="Code">The code the link carries.</param>
/// <param name="Url">The guest app's address with the code, when the API knows where the guest app is.</param>
public sealed record GuestLinkResponse(
    StayContextId StayId,
    string Code,
    string? Url);
