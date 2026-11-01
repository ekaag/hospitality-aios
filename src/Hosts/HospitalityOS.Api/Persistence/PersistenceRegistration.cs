using HospitalityOS.Adapters.EfCore.PostgreSql;
using HospitalityOS.Adapters.EfCore.Sqlite;
using HospitalityOS.Adapters.InMemory;

namespace HospitalityOS.Api.Persistence;

/// <summary>
/// Chooses the persistence adapter from the <c>Persistence</c> configuration section (P5). Persistence is one choice
/// for the whole host, unlike per-operation bindings: every repository and the unit of work must share one store
/// so a commit stays atomic. An unknown provider or missing connection string stops the host at startup.
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
            "POSTGRESQL" => services.AddPostgreSqlPersistence(ConnectionString(section, "PostgreSql")),
            _ => throw new InvalidOperationException($"{SectionName}:Provider '{provider}' is unknown; use InMemory, Sqlite, or PostgreSql."),
        };
    }

    private static string ConnectionString(IConfigurationSection section, string provider) =>
        section["ConnectionString"] is { Length: > 0 } connectionString
            ? connectionString
            : throw new InvalidOperationException($"{SectionName}:ConnectionString is required for the {provider} provider.");
}
