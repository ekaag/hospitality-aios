using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Properties;

/// <summary>
/// A property's base information, normalized (trimmed, country code upper-cased).
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="OrganizationId">The owning organization.</param>
/// <param name="Name">Display name.</param>
/// <param name="TimeZone">IANA time zone.</param>
/// <param name="Address">Street address.</param>
/// <param name="ContactPhone">Front-desk phone number.</param>
/// <param name="ContactEmail">Front-desk email address.</param>
/// <param name="Version">Increases with every change; send it back in <c>If-Match</c> to update safely.</param>
public sealed record PropertyResponse(
    PropertyId Id,
    OrganizationId OrganizationId,
    string Name,
    string TimeZone,
    PropertyAddress Address,
    string? ContactPhone,
    string? ContactEmail,
    long Version);
