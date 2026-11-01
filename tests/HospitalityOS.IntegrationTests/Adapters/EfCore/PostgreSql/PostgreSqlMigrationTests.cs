using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Adapters.EfCore.PostgreSql;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HospitalityOS.IntegrationTests.Adapters.EfCore.PostgreSql;

/// <summary>
/// The PostgreSQL migrations are the schema (P12): they must describe the current model exactly, which needs no server,
/// and applying them to an up-to-date database must change nothing, which runs on the server
/// <c>HOSPITALITYOS_POSTGRES</c> names (skipped without it).
/// </summary>
public sealed class PostgreSqlMigrationTests : IAsyncDisposable
{
    private const string ServerVariable = "HOSPITALITYOS_POSTGRES";

    /// <summary>
    /// Set where a server must be there (CI), so a missing one fails the tests instead of skipping them.
    /// </summary>
    private const string RequiredVariable = "HOSPITALITYOS_POSTGRES_REQUIRED";

    private readonly string? server = Environment.GetEnvironmentVariable(ServerVariable);
    private readonly string name = $"migrations_{Guid.NewGuid():N}";
    private readonly ServiceProvider services;
    private bool created;

    private static bool Required => string.Equals(Environment.GetEnvironmentVariable(RequiredVariable), "true", StringComparison.OrdinalIgnoreCase);

    public PostgreSqlMigrationTests()
    {
        ServiceCollection collection = new();
        collection.AddScoped<ICurrentTenant, FixedTenant>();
        collection.AddScoped<IEventMessageFactory, NoEvents>();
        collection.AddPostgreSqlPersistence(new NpgsqlConnectionStringBuilder(server ?? "Host=unset") { Database = name }.ConnectionString);
        services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void Model_HasNoChangesMissingFromMigrations()
    {
        using IServiceScope scope = services.CreateScope();

        bool pending = scope.ServiceProvider.GetRequiredService<HospitalityDbContext>().Database.HasPendingModelChanges();

        Assert.False(pending, "The model changed without a migration: run `dotnet ef migrations add <Name>` for HospitalityOS.Adapters.EfCore.PostgreSql.");
    }

    [Fact]
    public async Task Migrate_OnUpToDateDatabase_IsNoOp()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(server) && !Required, $"Set {ServerVariable} to a PostgreSQL server to run the PostgreSQL migration tests.");
        Assert.False(string.IsNullOrWhiteSpace(server), $"{RequiredVariable} is set, so {ServerVariable} must name a PostgreSQL server.");

        using IServiceScope scope = services.CreateScope();
        DatabaseFacade database = scope.ServiceProvider.GetRequiredService<HospitalityDbContext>().Database;
        created = true;
        await database.MigrateAsync(TestContext.Current.CancellationToken);
        IEnumerable<string> applied = await database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        await database.MigrateAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(applied, await database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        await services.DisposeAsync();

        if (created)
        {
            NpgsqlConnection.ClearAllPools();
            await using NpgsqlConnection connection = new(server);
            await connection.OpenAsync();
            await using NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class FixedTenant : ICurrentTenant
    {
        public TenantId TenantId { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209900001"));
    }

    private sealed class NoEvents : IEventMessageFactory
    {
        public EventMessage Create(CommittedEvent committed) => throw new InvalidOperationException("These tests commit nothing.");
    }
}
