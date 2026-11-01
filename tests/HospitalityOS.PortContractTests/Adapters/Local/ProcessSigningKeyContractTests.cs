using HospitalityOS.Adapters.Local;
using HospitalityOS.PortContractTests.Security;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.PortContractTests.Adapters.Local;

public sealed class ProcessSigningKeyContractTests : SigningKeyContractTests
{
    [Fact]
    public async Task EachProcessKey_IsItsOwn()
    {
        SigningPublicKey first = await CreateKey().GetPublicKeyAsync(Cancellation);
        SigningPublicKey second = await CreateKey().GetPublicKeyAsync(Cancellation);

        Assert.NotEqual(first.KeyId, second.KeyId);
    }

    protected override ISigningKey CreateKey()
    {
        ServiceProvider services = new ServiceCollection().AddLocalAdapters().BuildServiceProvider();
        return services.GetServices<IAdapterFactory<ISigningKey>>().Single(factory => factory.AdapterName == "Process").Create(services, new ConfigurationBuilder().Build());
    }
}
