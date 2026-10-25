namespace HospitalityOS.Domain.Guests.Guests;

/// <summary>
/// The little the platform keeps about a guest. Deliberately minimal personal data; contact details stay
/// with the messaging channel and the PMS (via <c>ExternalReference</c>), not here.
/// </summary>
/// <param name="DisplayName">How the concierge addresses the guest, e.g. <c>Alex</c>; optional.</param>
/// <param name="PreferredLanguage">Language tag such as <c>en</c> or <c>pt-BR</c>; optional.</param>
public sealed record GuestProfile(string? DisplayName = null, string? PreferredLanguage = null)
{
    /// <summary>
    /// A guest known only by ID, e.g. an anonymous web chat visitor.
    /// </summary>
    public static GuestProfile Anonymous { get; } = new();
}
