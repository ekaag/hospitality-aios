using HospitalityOS.Adapters.EfCore.Sqlite;
using HospitalityOS.PortContractTests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.EfCore.Sqlite;

public sealed class SqliteRepositoryContractTests : RepositoryContractTests
{
    private readonly SqliteTestDatabase database = new();

    protected override void AddPersistence(IServiceCollection services) => services.AddSqlitePersistence(database.ConnectionString);

    protected override void InitializeStore(IServiceProvider services) => SqliteTestDatabase.Migrate(services);

    protected override ValueTask DisposeStoreAsync() => database.DisposeAsync();
}
