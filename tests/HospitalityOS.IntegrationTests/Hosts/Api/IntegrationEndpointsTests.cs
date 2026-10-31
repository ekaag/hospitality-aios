using System.Net;
using HospitalityOS.Contracts.Api.Integrations;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// <c>/api/v1/integrations</c> (J4, spec §34 Integrations): an administrator sees each of a property's connectors with its
/// provider and health, the platform's port bindings with the tenant's own overrides but not other tenants', and the model
/// each AI task uses, all by name; settings, which may name secrets, never appear. Only administrators read it.
/// </summary>
public sealed class IntegrationEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/integrations";

    private static readonly Guid otherTenant = Guid.Parse("bb000000-0000-4000-8000-000000000001");

    private readonly StaffApi api = new(seedAuroraGrand: true, configure: Overrides);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AnAdministrator_SeesWhatThePropertyAndPlatformUse_ByNameOnly()
    {
        using HttpClient admin = api.Staff(Seed.TenantId);

        HttpResponseMessage response = await admin.GetAsync($"{Route}?propertyId={Seed.PropertyId}", Cancellation);
        string body = await response.Content.ReadAsStringAsync(Cancellation);
        IntegrationsResponse integrations = await StaffApi.OkAsync<IntegrationsResponse>(response, Cancellation);

        Assert.Equal(Enum.GetValues<ConnectorKind>(), integrations.Connectors.Select(connector => connector.Kind));
        ConnectorResponse pms = Assert.Single(integrations.Connectors, connector => connector.Kind == ConnectorKind.Pms);
        Assert.Equal(("MockPms", ConnectorStatus.Active, (bool?)true), (pms.Provider, pms.Status, pms.Healthy));
        Assert.Contains("MockPms", pms.AvailableProviders);

        Assert.Contains(new PortBindingResponse("EventTransport", "Default", "InMemory"), integrations.Ports);
        Assert.Contains(new PortBindingResponse("RealtimeNotifier", "Default", "SignalR"), integrations.Ports);
        Assert.Contains(new PortBindingResponse("Scheduler", "Tenant", "InProcess"), integrations.Ports);
        Assert.DoesNotContain(integrations.Ports, port => port.Port == "SearchIndex" && port.Scope == "Tenant");

        InferenceTaskResponse concierge = Assert.Single(integrations.InferenceTasks, task => task.Task == "concierge.answer");
        Assert.Equal(("ChatModel", "fake", "Fake", "fake-concierge"), (concierge.Capability, concierge.Provider, concierge.Adapter, concierge.Model));

        Assert.DoesNotContain("ANTHROPIC_API_KEY", body, StringComparison.Ordinal);
        Assert.DoesNotContain("settings", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OnlyAdministrators_ReadIt_ForAPropertyOfTheirs()
    {
        using HttpClient manager = api.Staff(Seed.TenantId, "Manager");
        using HttpClient admin = api.Staff(Seed.TenantId);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"{Route}?propertyId={Seed.PropertyId}", Cancellation)).StatusCode);
        Assert.Equal((HttpStatusCode.NotFound, "property.not_found"), await StaffApi.ProblemAsync(await admin.GetAsync($"{Route}?propertyId={Guid.NewGuid()}", Cancellation), Cancellation));
    }

    // The tenant's own override, which it sees, and another tenant's, which it does not.
    private static void Overrides(IWebHostBuilder builder)
    {
        builder.UseSetting($"Bindings:Scheduler:Tenants:{Seed.TenantId.Value}:Default:Adapter", "InProcess");
        builder.UseSetting($"Bindings:SearchIndex:Tenants:{otherTenant}:Default:Adapter", "InMemory");
    }
}
