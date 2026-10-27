using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.IntegrationRuntime;
using HospitalityOS.IntegrationRuntime.Configuration;
using HospitalityOS.IntegrationRuntime.Health;
using HospitalityOS.IntegrationRuntime.References;
using HospitalityOS.IntegrationRuntime.Resolution;
using HospitalityOS.IntegrationTests.Capabilities.Reading;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.IntegrationRuntime;

/// <summary>
/// The integration runtime in the real host: each property's configuration picks a registered provider per connector,
/// a provider swap changes only configuration while canonical IDs and their external references stay (Journey 18),
/// and misconfiguration is reported, never thrown.
/// </summary>
public sealed class IntegrationRuntimeTests : IAsyncLifetime
{
    private const string StayContext = "StayContext";

    private static readonly Dictionary<string, string> auroraPms = new() { ["hotelCode"] = "AG01" };

    private readonly AuroraGrand aurora = new(new TenantId(Guid.CreateVersion7()));
    private readonly WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Eventing:Dispatcher:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IIntegrationProvider<IPmsConnector>>(new TestPmsProvider("TestPms"));
            services.AddSingleton<IIntegrationProvider<IPmsConnector>>(new TestPmsProvider("OperaCloud"));
            services.AddSingleton<IIntegrationProvider<IMessagingConnector>, TestMessagingProvider>();
        });
    });

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private PropertyId Property => aurora.PropertyId;

    public async ValueTask InitializeAsync() => await aurora.SaveAsync(factory.Services, Cancellation);

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task Resolve_WhenNothingIsConfigured_IsNotConfigured()
    {
        Error error = await ResolvePmsFailureAsync();

        Assert.Equal((IntegrationRuntimeErrors.NotConfiguredCode, ErrorKind.NotFound), (error.Code, error.Kind));
    }

    [Fact]
    public async Task Configure_ThenResolve_GivesTheProvidersConnectorForThePropertyUntilItChanges()
    {
        await ConfigurePmsAsync("testpms", auroraPms);

        ResolvedConnector<IPmsConnector> first = await ResolvePmsAsync();
        ResolvedConnector<IPmsConnector> second = await ResolvePmsAsync();

        TestPmsConnector connector = Assert.IsType<TestPmsConnector>(first.Connector);
        Assert.Equal(("TestPms", "TestPms", Property, "AG01"), (first.ProviderName, connector.ProviderName, connector.Context.PropertyId, connector.Context.Settings["hotelCode"]));
        Assert.Same(first.Connector, second.Connector);
    }

    [Fact]
    public async Task SwappingProvider_ChangesOnlyConfiguration_AndCanonicalIdsKeepEveryProvidersReferences()
    {
        Guid alexStay = Guid.CreateVersion7();
        await ConfigurePmsAsync("TestPms", auroraPms);
        await LinkStayAsync("TestPms", "RES-0412", alexStay);

        await ConfigurePmsAsync("OperaCloud", new Dictionary<string, string> { ["hotelCode"] = "8812" });
        ResolvedConnector<IPmsConnector> swapped = await ResolvePmsAsync();
        await LinkStayAsync(swapped.ProviderName, "8812-0007", alexStay);

        Assert.Equal(("OperaCloud", "8812"), (Assert.IsType<TestPmsConnector>(swapped.Connector).ProviderName, ((TestPmsConnector)swapped.Connector).Context.Settings["hotelCode"]));
        await InTenantAsync(async services =>
        {
            IExternalReferences references = services.GetRequiredService<IExternalReferences>();
            Assert.Equal(alexStay, await references.FindCanonicalIdAsync(Property, "TestPms", StayContext, new ExternalId("RES-0412"), Cancellation));
            Assert.Equal(alexStay, await references.FindCanonicalIdAsync(Property, "OperaCloud", StayContext, new ExternalId("8812-0007"), Cancellation));
            IReadOnlyList<ExternalReference> all = await references.ListForCanonicalAsync(Property, StayContext, alexStay, Cancellation);
            Assert.Equal([("OperaCloud", "8812-0007"), ("TestPms", "RES-0412")], all.Select(reference => (reference.ProviderName, reference.ExternalId)).Order());
        });
    }

    [Fact]
    public async Task Configure_WithAnUnregisteredProvider_ListsTheRegisteredOnes()
    {
        Result<IntegrationConfigurationId> result = await TryConfigurePmsAsync("Protel", auroraPms);

        FieldError field = Assert.Single(Assert.IsType<ValidationError>(result.Error).Fields);
        Assert.Equal(("providerName", IntegrationRuntimeErrors.ProviderUnknownCode), (field.Field, field.Code));
        Assert.Equal("No Pms provider named 'Protel' is registered. Registered: MockPms, OperaCloud, TestPms.", field.Message);
    }

    [Fact]
    public async Task Configure_WithSettingsTheProviderRejects_IsInvalid()
    {
        Result<IntegrationConfigurationId> result = await TryConfigurePmsAsync("TestPms", new Dictionary<string, string> { ["region"] = "eu" });

        FieldError field = Assert.Single(Assert.IsType<ValidationError>(result.Error).Fields);
        Assert.Equal(("settings", IntegrationRuntimeErrors.SettingsInvalidCode, "hotelCode is required."), (field.Field, field.Code, field.Message));
    }

    [Fact]
    public async Task Configure_AnotherTenantsProperty_IsNotFoundAndInvisible()
    {
        await ConfigurePmsAsync("TestPms", auroraPms);
        TenantId otherTenant = new(Guid.CreateVersion7());

        Result<IntegrationConfigurationId> configure = await InTenantAsync(
            services => services.GetRequiredService<IIntegrationConfigurator>().ConfigureAsync(Property, ConnectorKind.Pms, "TestPms", auroraPms, Cancellation),
            otherTenant);
        Result<ResolvedConnector<IPmsConnector>> resolve = await InTenantAsync(
            services => services.GetRequiredService<IIntegrationResolver<IPmsConnector>>().ResolveAsync(Property, Cancellation),
            otherTenant);

        Assert.Equal("property.not_found", configure.Error.Code);
        Assert.Equal(IntegrationRuntimeErrors.NotConfiguredCode, resolve.Error.Code);
    }

    [Fact]
    public async Task Disable_StopsResolution_AndEnableRestoresIt()
    {
        await ConfigurePmsAsync("TestPms", auroraPms);

        await SetPmsEnabledAsync(false);
        Error disabled = await ResolvePmsFailureAsync();
        await SetPmsEnabledAsync(true);
        ResolvedConnector<IPmsConnector> enabled = await ResolvePmsAsync();

        Assert.Equal((IntegrationRuntimeErrors.DisabledCode, ErrorKind.InvalidState), (disabled.Code, disabled.Kind));
        Assert.Equal("TestPms", enabled.ProviderName);
    }

    [Fact]
    public async Task Resolve_AProviderNoLongerRegistered_IsUnavailable()
    {
        await SaveConfigurationAsync(ConnectorKind.Pms, "RetiredPms");

        Error error = await ResolvePmsFailureAsync();

        Assert.Equal((IntegrationRuntimeErrors.ProviderUnavailableCode, "Pms provider 'RetiredPms' is not registered in this deployment."), (error.Code, error.Message));
    }

    [Fact]
    public async Task Health_ReportsEveryConfiguredConnectorByKind()
    {
        await ConfigurePmsAsync("TestPms", auroraPms);
        await SaveConfigurationAsync(ConnectorKind.Messaging, "TestMessaging", ("health", "down"));
        await SaveConfigurationAsync(ConnectorKind.Cms, "NativeCms", disabled: true);

        IReadOnlyList<IntegrationHealthReport> reports = await InTenantAsync(services => services.GetRequiredService<IIntegrationHealth>().CheckAsync(Property, Cancellation));

        Assert.Equal(
            [
                (ConnectorKind.Pms, "TestPms", IntegrationStatus.Active, true, (string?)null),
                (ConnectorKind.Cms, "NativeCms", IntegrationStatus.Disabled, true, "Disabled; not checked."),
                (ConnectorKind.Messaging, "TestMessaging", IntegrationStatus.Active, false, "Messaging gateway down"),
            ],
            reports.Select(report => (report.Kind, report.ProviderName, report.Status, report.Health.IsHealthy, report.Health.Description)));
    }

    [Fact]
    public async Task Link_AgainToTheSameRecordReturnsIt_ButToAnotherRecordConflicts()
    {
        Guid alexStay = Guid.CreateVersion7();
        ExternalReference linked = await LinkStayAsync("TestPms", "RES-0412", alexStay);

        ExternalReference again = await LinkStayAsync("TestPms", "RES-0412", alexStay);
        Result<ExternalReference> conflict = await InTenantAsync(services => services.GetRequiredService<IExternalReferences>()
            .LinkAsync(Property, "TestPms", StayContext, new ExternalId("RES-0412"), Guid.CreateVersion7(), Cancellation));

        Assert.Equal(linked.Id, again.Id);
        Assert.Equal((IntegrationRuntimeErrors.ReferenceConflictCode, ErrorKind.Conflict), (conflict.Error.Code, conflict.Error.Kind));
    }

    [Fact]
    public async Task ListProviders_ReturnsTheRegisteredProvidersOfAKindIncludingTheHostsMocks()
    {
        IReadOnlyList<string> pms = await InTenantAsync(services => Task.FromResult(services.GetRequiredService<IIntegrationConfigurator>().ListProviders(ConnectorKind.Pms)));
        IReadOnlyList<string> pos = await InTenantAsync(services => Task.FromResult(services.GetRequiredService<IIntegrationConfigurator>().ListProviders(ConnectorKind.Pos)));

        Assert.Equal(["MockPms", "OperaCloud", "TestPms"], pms);
        Assert.Equal(["MockPos"], pos);
    }

    private async Task ConfigurePmsAsync(string providerName, IReadOnlyDictionary<string, string> settings)
    {
        Result<IntegrationConfigurationId> result = await TryConfigurePmsAsync(providerName, settings);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
    }

    private Task<Result<IntegrationConfigurationId>> TryConfigurePmsAsync(string providerName, IReadOnlyDictionary<string, string> settings) =>
        InTenantAsync(
            services => services.GetRequiredService<IIntegrationConfigurator>().ConfigureAsync(Property, ConnectorKind.Pms, providerName, settings, Cancellation),
            commit: true);

    private async Task SetPmsEnabledAsync(bool enabled)
    {
        Result result = await InTenantAsync(
            services => services.GetRequiredService<IIntegrationConfigurator>().SetEnabledAsync(Property, ConnectorKind.Pms, enabled, Cancellation),
            commit: true);

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// Saves a configuration straight through the repository, bypassing the configurator's provider checks: as if the
    /// provider was registered when configured and has since been removed, or is not part of this test host.
    /// </summary>
    private async Task SaveConfigurationAsync(ConnectorKind kind, string providerName, (string Name, string Value)? setting = null, bool disabled = false) =>
        await InTenantAsync(
            async services =>
            {
                IntegrationConfiguration configuration = IntegrationConfiguration.Create(
                    new IntegrationConfigurationId(Guid.CreateVersion7()),
                    aurora.TenantId,
                    Property,
                    kind,
                    providerName,
                    setting is { } value ? [new IntegrationSetting(value.Name, value.Value)] : [],
                    AuroraGrand.Now).Value;

                if (disabled)
                {
                    configuration.Disable(AuroraGrand.Now);
                }

                await services.GetRequiredService<IIntegrationConfigurationRepository>().AddAsync(configuration, Cancellation);

                return Result.Success();
            },
            commit: true);

    private async Task<ExternalReference> LinkStayAsync(string providerName, string externalId, Guid stay)
    {
        Result<ExternalReference> result = await InTenantAsync(
            services => services.GetRequiredService<IExternalReferences>().LinkAsync(Property, providerName, StayContext, new ExternalId(externalId), stay, Cancellation),
            commit: true);

        return result.Value;
    }

    private async Task<ResolvedConnector<IPmsConnector>> ResolvePmsAsync()
    {
        Result<ResolvedConnector<IPmsConnector>> result = await InTenantAsync(
            services => services.GetRequiredService<IIntegrationResolver<IPmsConnector>>().ResolveAsync(Property, Cancellation));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        return result.Value;
    }

    private async Task<Error> ResolvePmsFailureAsync()
    {
        Result<ResolvedConnector<IPmsConnector>> result = await InTenantAsync(
            services => services.GetRequiredService<IIntegrationResolver<IPmsConnector>>().ResolveAsync(Property, Cancellation));

        Assert.True(result.IsFailure);

        return result.Error;
    }

    private async Task InTenantAsync(Func<IServiceProvider, Task> action) =>
        await InTenantAsync(async services =>
        {
            await action(services);

            return true;
        });

    /// <summary>
    /// Runs <paramref name="action"/> in a new scope acting for <paramref name="tenant"/> (Aurora's by default) and, when
    /// <paramref name="commit"/> and the action succeeded, commits what it staged.
    /// </summary>
    private async Task<T> InTenantAsync<T>(Func<IServiceProvider, Task<T>> action, TenantId? tenant = null, bool commit = false)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant ?? aurora.TenantId);

        T result = await action(scope.ServiceProvider);

        if (commit && result is not Result { IsFailure: true })
        {
            Result<CommitReceipt> committed = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation);
            Assert.True(committed.IsSuccess, committed.IsFailure ? committed.Error.Message : null);
        }

        return result;
    }
}
