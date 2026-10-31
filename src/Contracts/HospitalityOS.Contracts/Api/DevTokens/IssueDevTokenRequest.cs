namespace HospitalityOS.Contracts.Api.DevTokens;

/// <summary>
/// Asks for a token for one of the dev personas.
/// </summary>
/// <param name="Persona">The persona's key; required.</param>
public sealed record IssueDevTokenRequest(string? Persona = null);
