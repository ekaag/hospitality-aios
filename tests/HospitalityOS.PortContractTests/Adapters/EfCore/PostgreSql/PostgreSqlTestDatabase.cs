using HospitalityOS.Adapters.EfCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HospitalityOS.PortContractTests.Adapters.EfCore.PostgreSql;

/// <summary>
/// A database of its own for one test on the PostgreSQL server <see cref="ServerVariable"/> names (CI starts one as a
/// service; locally, set the variable to run these), created by the real migrations and dropped afterwards. Without the
/// variable, the test is skipped rather than failed.
/// </summary>
internal sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    /// <summary>
    /// A connection string to a server where the tests may create and drop databases, e.g.
    /// <c>Host=localhost;Username=postgres;Password=postgres</c>.
    /// </summary>
    public const string ServerVariable = "HOSPITALITYOS_POSTGRES";

    /// <summary>
    /// Set where a server must be there (CI), so a missing one fails the tests instead of skipping them.
    /// </summary>
    private const string RequiredVariable = "HOSPITALITYOS_POSTGRES_REQUIRED";

    private readonly string? server = Environment.GetEnvironmentVariable(ServerVariable);
    private readonly string name = $"contract_{Guid.NewGuid():N}";
    private bool created;

    private static bool Required => string.Equals(Environment.GetEnvironmentVariable(RequiredVariable), "true", StringComparison.OrdinalIgnoreCase);

    public PostgreSqlTestDatabase()
    {
        ConnectionString = new NpgsqlConnectionStringBuilder(server ?? "Host=unset") { Database = name }.ConnectionString;
    }

    public string ConnectionString { get; }

    /// <summary>
    /// Creates the database by migrating it, or skips the test when no server is set.
    /// </summary>
    public void Migrate(IServiceProvider services)
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(server) && !Required, $"Set {ServerVariable} to a PostgreSQL server to run the PostgreSQL contract tests.");
        Assert.False(string.IsNullOrWhiteSpace(server), $"{RequiredVariable} is set, so {ServerVariable} must name a PostgreSQL server.");

        created = true;
        using IServiceScope scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<HospitalityDbContext>().Database.Migrate();
    }

    public async ValueTask DisposeAsync()
    {
        if (!created)
        {
            return;
        }

        // Pooled connections keep the database open; FORCE ends any that remain.
        NpgsqlConnection.ClearAllPools();
        await using NpgsqlConnection connection = new(server);
        await connection.OpenAsync();
        await using NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }
}
