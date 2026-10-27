using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationRuntime.Providers;
using Xunit;

namespace HospitalityOS.UnitTests.IntegrationRuntime.Providers;

public sealed class ConnectorKindsTests
{
    [Fact]
    public void All_MapsEveryConnectorInterfaceToItsOwnKind()
    {
        IEnumerable<Type> connectors = typeof(ExternalId).Assembly.GetExportedTypes()
            .Where(type => type.IsInterface && type.Name.EndsWith("Connector", StringComparison.Ordinal));

        Assert.Equal(connectors.OrderBy(type => type.Name), ConnectorKinds.All.Keys.OrderBy(type => type.Name));
        Assert.Equal(Enum.GetValues<ConnectorKind>(), ConnectorKinds.All.Values.Order());
    }
}
