namespace HospitalityOS.Contracts.Api.Access;

/// <summary>
/// An operator invites a customer's first administrator, who invites everyone else.
/// </summary>
/// <param name="DisplayName">Who they are.</param>
public sealed record GrantFirstAdminRequest(string? DisplayName = null);
