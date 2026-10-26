using HospitalityOS.Adapters.InMemory;
using HospitalityOS.PortContractTests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.InMemory;

public sealed class InMemoryRepositoryQueryContractTests : RepositoryQueryContractTests
{
    protected override void AddPersistence(IServiceCollection services) => services.AddInMemoryPersistence();
}
