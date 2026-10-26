using HospitalityOS.Adapters.EfCore.Sqlite;
using HospitalityOS.PortContractTests.Auditing;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.EfCore.Sqlite;

public sealed class SqliteAuditLogContractTests : AuditLogContractTests
{
    private readonly SqliteTestDatabase database = new();

    protected override void AddPersistence(IServiceCollection services) => services.AddSqlitePersistence(database.ConnectionString);

    protected override void InitializeStore(IServiceProvider services) => SqliteTestDatabase.Migrate(services);

    protected override ValueTask DisposeStoreAsync() => database.DisposeAsync();
}
