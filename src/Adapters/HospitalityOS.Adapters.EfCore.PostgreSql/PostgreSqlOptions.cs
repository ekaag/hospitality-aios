namespace HospitalityOS.Adapters.EfCore.PostgreSql;

/// <summary>
/// How the app reaches PostgreSQL.
/// </summary>
public sealed class PostgreSqlOptions
{
    /// <summary>
    /// An Npgsql connection string, e.g. <c>Host=localhost;Database=hospitalityos;Username=app;Password=...</c>; with
    /// <see cref="EntraSignIn"/>, no password: the user is the Entra identity's role.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Whether to sign in as the app's Microsoft Entra identity (in Azure, its managed identity, which
    /// <c>AZURE_CLIENT_ID</c> names) rather than with the connection string's password: an access token is the password,
    /// renewed before it expires.
    /// </summary>
    public bool EntraSignIn { get; init; }

    /// <summary>
    /// The database role the app runs as, which the migration step (signed in as the server's administrator) creates for
    /// an Entra identity and grants the app's tables to; for an Entra identity, the identity's name, e.g.
    /// <c>identity-api-dev-uksouth-id</c>. <c>null</c> when the signed-in user is the app's own (local servers).
    /// </summary>
    public string? AppRoleName { get; init; }

    /// <summary>
    /// The app role's Entra object (principal) ID, for creating its role; ignored with password sign-in, where the role
    /// must already exist.
    /// </summary>
    public Guid AppRoleObjectId { get; init; }
}
