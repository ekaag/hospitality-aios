using System.Text.RegularExpressions;
using HospitalityOS.Adapters.EfCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalityOS.Adapters.EfCore.PostgreSql.Persistence;

/// <summary>
/// After the migrations, lets the app's role use what they made: for an Entra identity, first creates its role
/// (Azure's <c>pgaadauth_create_principal_with_oid</c>, in the <c>postgres</c> database, which only the server's Entra
/// administrator may call); then grants it connecting, and reading and writing every table, but no schema changes.
/// Migrations run as the administrator, so the app can never alter its own schema (P12). Idempotent.
/// </summary>
internal sealed partial class PostgreSqlAppRoleAccess(HospitalityDbContext context, PostgreSqlConnection connection) : ISchemaAccess
{
    public async Task GrantAsync(CancellationToken cancellationToken)
    {
        PostgreSqlAppRole role = connection.AppRole;
        string quoted = Quote(role.Name);

        if (connection.EntraSignIn)
        {
            await using NpgsqlDataSource server = PostgreSqlDataSources.Entra(connection.ConnectionString, "postgres");
            await using NpgsqlCommand exists = server.CreateCommand("SELECT 1 FROM pg_roles WHERE rolname = $1");
            exists.Parameters.Add(new NpgsqlParameter { Value = role.Name });

            if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                await using NpgsqlCommand create = server.CreateCommand("SELECT * FROM pgaadauth_create_principal_with_oid($1, $2, 'service', false, false)");
                create.Parameters.Add(new NpgsqlParameter { Value = role.Name });
                create.Parameters.Add(new NpgsqlParameter { Value = role.ObjectId.ToString() });
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        string database = Quote(context.Database.GetDbConnection().Database);
        string grants =
            $"GRANT CONNECT ON DATABASE {database} TO {quoted};" +
            $"GRANT USAGE ON SCHEMA public TO {quoted};" +
            $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {quoted};" +
            $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {quoted};";

        // Identifiers cannot be parameters; both are checked and quoted.
#pragma warning disable EF1002
        await context.Database.ExecuteSqlRawAsync(grants, cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002
    }

    /// <summary>
    /// A role or database name as a quoted identifier; only names a deployment makes (letters, digits, hyphens,
    /// underscores) are accepted, so nothing can break out of the quotes.
    /// </summary>
    public static string Quote(string name) =>
        Identifier().IsMatch(name) ? $"\"{name}\"" : throw new ArgumentException($"'{name}' is not a role or database name this deployment makes.", nameof(name));

    [GeneratedRegex("^[A-Za-z0-9_-]{1,63}$")]
    private static partial Regex Identifier();
}

/// <summary>
/// How the adapter connects, kept for the migration step's access grants.
/// </summary>
internal sealed record PostgreSqlConnection(string ConnectionString, bool EntraSignIn, PostgreSqlAppRole AppRole);

/// <summary>
/// The database role the app runs as, and its Entra object ID (used only to create an Entra identity's role).
/// </summary>
internal sealed record PostgreSqlAppRole(string Name, Guid ObjectId);
