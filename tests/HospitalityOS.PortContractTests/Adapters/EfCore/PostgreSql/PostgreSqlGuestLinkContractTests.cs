using HospitalityOS.Adapters.EfCore.PostgreSql;
using HospitalityOS.PortContractTests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.EfCore.PostgreSql;

public sealed class PostgreSqlGuestLinkContractTests : GuestLinkContractTests
{
    private readonly PostgreSqlTestDatabase database = new();

    protected override void AddPersistence(IServiceCollection services) => services.AddPostgreSqlPersistence(database.ConnectionString);

    protected override void InitializeStore(IServiceProvider services) => database.Migrate(services);

    protected override ValueTask DisposeStoreAsync() => database.DisposeAsync();
}
