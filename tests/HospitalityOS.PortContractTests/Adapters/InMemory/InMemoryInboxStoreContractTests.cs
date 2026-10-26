using HospitalityOS.Adapters.InMemory;
using HospitalityOS.PortContractTests.Eventing;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Adapters.InMemory;

public sealed class InMemoryInboxStoreContractTests : InboxStoreContractTests
{
    protected override void AddPersistence(IServiceCollection services) => services.AddInMemoryPersistence();
}
