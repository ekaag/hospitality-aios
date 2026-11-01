using HospitalityOS.Adapters.EfCore.PostgreSql;
using HospitalityOS.Adapters.EfCore.Sqlite;
using HospitalityOS.Adapters.InMemory;

namespace HospitalityOS.Api.Persistence;

/// <summary>
/// Chooses the persistence adapter from the <c>Persistence</c> configuration section (P5). Persistence is one choice
/// for the whole host, unlike per-operation bindings: every repository and the unit of work must share one store
/// so a commit stays atomic. An unknown provider or missing connection string stops the host at startup. PostgreSQL
/// also takes <c>SignIn</c> (<c>Password</c>, the default, or <c>Entra</c>) and, for the migration step, <c>AppRole:Name</c>
/// and <c>AppRole:ObjectId</c>: the role the API runs as, which the step creates and grants the tables to.
/// </summary>
internal static class PersistenceRegistration
{
    public const string SectionName = "Persistence";

    public static IServiceCollection AddConfiguredPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(SectionName);
        string provider = section["Provider"] ?? "InMemory";

        return provider.ToUpperInvariant() switch
        {
            "INMEMORY" => services.AddInMemoryPersistence(),
            "SQLITE" => services.AddSqlitePersistence(ConnectionString(section, "Sqlite")),
            "POSTGRESQL" => services.AddPostgreSqlPersistence(PostgreSql(section)),
            _ => throw new InvalidOperationException($"{SectionName}:Provider '{provider}' is unknown; use InMemory, Sqlite, or PostgreSql."),
        };
    }

    private static string ConnectionString(IConfigurationSection section, string provider) =>
        section["ConnectionString"] is { Length: > 0 } connectionString
            ? connectionString
            : throw new InvalidOperationException($"{SectionName}:ConnectionString is required for the {provider} provider.");

    private static PostgreSqlOptions PostgreSql(IConfigurationSection section)
    {
        string? role = section["AppRole:Name"];
        Guid objectId = Guid.Empty;

        if (!string.IsNullOrEmpty(role) && !Guid.TryParse(section["AppRole:ObjectId"], out objectId))
        {
            throw new InvalidOperationException($"{SectionName}:AppRole:ObjectId must be the role's Entra object ID when {SectionName}:AppRole:Name is set.");
        }

        return new PostgreSqlOptions
        {
            ConnectionString = ConnectionString(section, "PostgreSql"),
            EntraSignIn = section["SignIn"] switch
            {
                null or "" => false,
                string value when string.Equals(value, "Password", StringComparison.OrdinalIgnoreCase) => false,
                string value when string.Equals(value, "Entra", StringComparison.OrdinalIgnoreCase) => true,
                string value => throw new InvalidOperationException($"{SectionName}:SignIn '{value}' is unknown; use Password or Entra."),
            },
            AppRoleName = string.IsNullOrEmpty(role) ? null : role,
            AppRoleObjectId = objectId,
        };
    }
}
