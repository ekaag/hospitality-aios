namespace HospitalityOS.IntegrationContracts.Pms;

/// <summary>
/// The guest on a stay. Deliberately the same minimal personal data as the canonical guest profile (P10): contact
/// details stay with the PMS and the messaging provider.
/// </summary>
/// <param name="Id">The provider's guest or profile ID.</param>
/// <param name="DisplayName">How the concierge addresses the guest; optional.</param>
/// <param name="PreferredLanguage">Language tag such as <c>en</c> or <c>pt-BR</c>; optional.</param>
public sealed record PmsGuest(ExternalId Id, string? DisplayName = null, string? PreferredLanguage = null);
