namespace HospitalityOS.Contracts.Api.DevTokens;

/// <summary>
/// Someone from the reference hotels' seed a local developer can sign in as.
/// </summary>
/// <param name="Id">The persona's key, e.g. <c>guest-alex</c>; send it to have a token issued.</param>
/// <param name="DisplayName">Their name.</param>
/// <param name="Description">Who they are, e.g. the in-house guest in room 412.</param>
/// <param name="Role">Their role: <c>Guest</c>, or a staff role (<c>Worker</c>, <c>Manager</c>, <c>Admin</c>).</param>
/// <param name="PropertyId">The hotel they belong to (a guest's stay, a worker's roster), or null for someone who works
/// across the tenant's hotels (managers and administrators).</param>
/// <param name="PropertyName">That hotel's name, e.g. <c>Mysore Grand</c>.</param>
public sealed record DevPersona(string Id, string DisplayName, string Description, string Role, Guid? PropertyId = null, string? PropertyName = null);
