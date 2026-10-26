using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Adapters.EfCore.Sqlite;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Adapters.EfCore.Sqlite;

/// <summary>
/// The SQLite migrations are the schema (P12): they must describe the current model exactly, and applying them to
/// an up-to-date database must change nothing.
/// </summary>
public sealed class SqliteMigrationTests : IAsyncDisposable
{
    private readonly string connectionString = $"Data Source=migrations-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection keepAlive;
    private readonly ServiceProvider services;

    public SqliteMigrationTests()
    {
        keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();

        ServiceCollection collection = new();
        collection.AddScoped<ICurrentTenant, FixedTenant>();
        collection.AddScoped<IEventMessageFactory, NoEvents>();
        collection.AddSqlitePersistence(connectionString);
        services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void Model_HasNoChangesMissingFromMigrations()
    {
        using IServiceScope scope = services.CreateScope();

        bool pending = scope.ServiceProvider.GetRequiredService<HospitalityDbContext>().Database.HasPendingModelChanges();

        Assert.False(pending, "The model changed without a migration: run `dotnet ef migrations add <Name>` for HospitalityOS.Adapters.EfCore.Sqlite.");
    }

    [Fact]
    public async Task Migrate_OnUpToDateDatabase_IsNoOp()
    {
        using IServiceScope scope = services.CreateScope();
        DatabaseFacade database = scope.ServiceProvider.GetRequiredService<HospitalityDbContext>().Database;
        await database.MigrateAsync(TestContext.Current.CancellationToken);
        IEnumerable<string> applied = await database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        await database.MigrateAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(applied, await database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        await services.DisposeAsync();
        await keepAlive.DisposeAsync();
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
