namespace HospitalityOS.Contracts.Api.DevTokens;

/// <summary>
/// A bearer token for a dev persona, valid only against this API while dev tokens are on.
/// </summary>
/// <param name="AccessToken">Send it as <c>Authorization: Bearer &lt;token&gt;</c>.</param>
/// <param name="ExpiresAt">When it stops working (UTC).</param>
/// <param name="Persona">Who it signs in as.</param>
public sealed record DevTokenResponse(string AccessToken, DateTimeOffset ExpiresAt, DevPersona Persona);
