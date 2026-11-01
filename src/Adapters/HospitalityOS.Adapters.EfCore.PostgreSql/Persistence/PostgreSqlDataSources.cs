using Azure.Core;
using Azure.Identity;
using Npgsql;

namespace HospitalityOS.Adapters.EfCore.PostgreSql.Persistence;

/// <summary>
/// Connections that sign in as the app's Microsoft Entra identity: each new physical connection's password is an
/// access token for Azure Database for PostgreSQL, fetched in the background and renewed well before it expires (tokens
/// last about an hour), so opening a connection never waits for Entra.
/// </summary>
internal static class PostgreSqlDataSources
{
    /// <summary>
    /// The token audience Azure Database for PostgreSQL accepts.
    /// </summary>
    public const string Scope = "https://ossrdbms-aad.database.windows.net/.default";

    private static readonly Lazy<TokenCredential> credential = new(() => new DefaultAzureCredential());

    public static NpgsqlDataSource Entra(string connectionString, string? database = null)
    {
        NpgsqlConnectionStringBuilder settings = new(connectionString);

        if (database is not null)
        {
            settings.Database = database;
        }

        NpgsqlDataSourceBuilder builder = new(settings.ConnectionString);
        builder.UsePeriodicPasswordProvider(
            async (_, cancellationToken) => (await credential.Value.GetTokenAsync(new TokenRequestContext([Scope]), cancellationToken).ConfigureAwait(false)).Token,
            successRefreshInterval: TimeSpan.FromMinutes(30),
            failureRefreshInterval: TimeSpan.FromSeconds(10));

        return builder.Build();
    }
}
