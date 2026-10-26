using HospitalityOS.Adapters.InMemory;
using HospitalityOS.PortContractTests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.InMemory;

public sealed class InMemoryRepositoryContractTests : RepositoryContractTests
{
    protected override void AddPersistence(IServiceCollection services) => services.AddInMemoryPersistence();
}
