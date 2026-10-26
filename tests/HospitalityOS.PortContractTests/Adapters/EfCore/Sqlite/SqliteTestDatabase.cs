using HospitalityOS.Adapters.EfCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.EfCore.Sqlite;

/// <summary>
/// A private in-memory SQLite database for one test, built by the real migrations. Shared-cache memory databases
/// live while a connection is open, so this one stays open until the test ends.
/// </summary>
internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection keepAlive;

    public SqliteTestDatabase()
    {
        ConnectionString = $"Data Source=contract-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        keepAlive = new SqliteConnection(ConnectionString);
        keepAlive.Open();
    }

    public string ConnectionString { get; }

    public static void Migrate(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<HospitalityDbContext>().Database.Migrate();
    }

    public ValueTask DisposeAsync() => keepAlive.DisposeAsync();
}
