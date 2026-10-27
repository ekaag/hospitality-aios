using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationRuntime.Providers;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.UnitTests.IntegrationRuntime.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.IntegrationRuntime.Providers;

public sealed class ProviderRegistryTests
{
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209908001"));
    private static readonly PropertyId auroraGrand = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209908002"));
    private static readonly DateTimeOffset now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private readonly FakePmsProvider mockPms = new("MockPms");
    private readonly FakePmsProvider operaCloud = new("OperaCloud");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void Constructor_ReadsKindAndOrdersProviderNames()
    {
        ProviderRegistry<IPmsConnector> registry = new([operaCloud, mockPms]);

        Assert.Equal(ConnectorKind.Pms, registry.Kind);
        Assert.Equal(["MockPms", "OperaCloud"], registry.ProviderNames);
    }

    [Fact]
    public void Constructor_WithProviderNamesDifferingOnlyInCase_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ProviderRegistry<IPmsConnector>([mockPms, new FakePmsProvider("MOCKPMS")]));
    }

    [Fact]
    public void Constructor_ForANonConnectorType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ProviderRegistry<IDisposable>([]));
    }

    [Fact]
    public void FindProviderName_IgnoresCaseAndReturnsTheRegisteredSpelling()
    {
        ProviderRegistry<IPmsConnector> registry = new([mockPms]);

        Assert.Equal(("MockPms", null), (registry.FindProviderName("mockpms"), registry.FindProviderName("OperaCloud")));
    }

    [Fact]
    public void GetConnector_CreatesOnceWithThePropertysSettingsByNameIgnoringCase()
    {
        ProviderRegistry<IPmsConnector> registry = new([mockPms]);
        IntegrationConfiguration configuration = Configure("MockPms");

        IPmsConnector first = registry.GetConnector(configuration)!;
        IPmsConnector second = registry.GetConnector(configuration)!;

        FakePmsConnector connector = Assert.IsType<FakePmsConnector>(first);
        Assert.Same(first, second);
        Assert.Equal(1, mockPms.Created);
        Assert.Equal((tenantId, auroraGrand, "AG01"), (connector.Context.TenantId, connector.Context.PropertyId, connector.Context.Settings["HOTELCODE"]));
    }

    [Fact]
    public void GetConnector_AfterTheConfigurationChanges_CreatesTheNewProvidersConnector()
    {
        ProviderRegistry<IPmsConnector> registry = new([mockPms, operaCloud]);
        IntegrationConfiguration configuration = Configure("MockPms");
        IPmsConnector before = registry.GetConnector(configuration)!;

        configuration.Reconfigure("OperaCloud", [new("hotelCode", "8812")], now);
        IPmsConnector after = registry.GetConnector(configuration)!;

        Assert.NotSame(before, after);
        Assert.Equal(("OperaCloud", "8812"), (Assert.IsType<FakePmsConnector>(after).ProviderName, ((FakePmsConnector)after).Context.Settings["hotelCode"]));
    }

    [Fact]
    public void GetConnector_ForAnUnregisteredProvider_ReturnsNull()
    {
        ProviderRegistry<IPmsConnector> registry = new([mockPms]);

        Assert.Null(registry.GetConnector(Configure("RetiredPms")));
    }

    [Fact]
    public async Task CheckHealth_ReportsTheProbeAProviderFailureOrAnUnregisteredProvider()
    {
        ProviderRegistry<IPmsConnector> registry = new([
            new FakePmsProvider("SickPms", () => AdapterHealth.Unhealthy("Login refused")),
            new FakePmsProvider("BrokenPms", () => throw new InvalidOperationException("PMS unreachable")),
            mockPms,
        ]);

        AdapterHealth healthy = await registry.CheckHealthAsync(Configure("MockPms"), Cancellation);
        AdapterHealth sick = await registry.CheckHealthAsync(Configure("SickPms"), Cancellation);
        AdapterHealth broken = await registry.CheckHealthAsync(Configure("BrokenPms"), Cancellation);
        AdapterHealth unregistered = await registry.CheckHealthAsync(Configure("RetiredPms"), Cancellation);

        Assert.True(healthy.IsHealthy);
        Assert.Equal((false, "Login refused"), (sick.IsHealthy, sick.Description));
        Assert.Equal((false, "PMS unreachable"), (broken.IsHealthy, broken.Description));
        Assert.Equal((false, "Pms provider 'RetiredPms' is not registered in this deployment."), (unregistered.IsHealthy, unregistered.Description));
    }

    private static IntegrationConfiguration Configure(string providerName) =>
        IntegrationConfiguration.Create(
            new IntegrationConfigurationId(Guid.CreateVersion7()), tenantId, auroraGrand, ConnectorKind.Pms, providerName, [new("hotelCode", "AG01")], now).Value;
}
