using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.Integrations.Mock.Pms;
using HospitalityOS.PortContractTests.Integrations;
using HospitalityOS.SharedKernel.Ids;
using Microsoft.Extensions.Options;

namespace HospitalityOS.PortContractTests.Adapters.Integrations;

public sealed class MockPmsConnectorContractTests : PmsConnectorContractTests
{
    protected override IPmsConnector CreateConnector(PropertyId propertyId, IReadOnlyDictionary<PropertyId, IReadOnlyList<PmsStay>> stays)
    {
        MockPmsOptions options = new();

        foreach ((PropertyId property, IReadOnlyList<PmsStay> propertyStays) in stays)
        {
            options.Stays[property] = [.. propertyStays];
        }

        return new MockPmsProvider(Options.Create(options)).Create(new ConnectorContext(ConnectorTestData.Tenant, propertyId, new Dictionary<string, string>()));
    }
}
