using System.Text.Json;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The whole backbone in the real host (P1): a commit records the event, the dispatcher publishes it, and a
/// subscriber receives the canonical envelope.
/// </summary>
public sealed class EventFlowTests
{
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209900001"));

    [Fact]
    public async Task Commit_AggregateChange_IsPublishedToSubscribersAsCanonicalEnvelope()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Eventing:Dispatcher:PollInterval", "00:00:00.050");
            builder.ConfigureTestServices(services => services.AddScoped<ICurrentTenant, FixedTenant>());
        });
        IEventTransport transport = factory.Services.GetRequiredService<IBindingResolver<IEventTransport>>().Resolve();
        TaskCompletionSource<EventMessage> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using IAsyncDisposable subscription = await transport.SubscribeAsync(
            "event-flow-test",
            (message, _) =>
            {
                received.TrySetResult(message);

                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);
        OrganizationId organizationId = new(Guid.CreateVersion7());

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            Organization organization = Organization.Create(organizationId, tenantId, "Aurora Hotels", DateTimeOffset.UtcNow).Value;
            await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>().AddAsync(organization, TestContext.Current.CancellationToken);
            Assert.True((await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(TestContext.Current.CancellationToken)).IsSuccess);
        }

        EventMessage message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(("OrganizationCreated", 1, tenantId, "Organization", organizationId.Value), (message.EventType, message.EventVersion, message.TenantId, message.AggregateType, message.AggregateId));
        using JsonDocument envelope = JsonDocument.Parse(message.Envelope);
        Assert.Equal(message.EventId, envelope.RootElement.GetProperty("eventId").GetGuid());
        Assert.Equal(organizationId.Value, envelope.RootElement.GetProperty("organizationId").GetGuid());
        Assert.Equal("Aurora Hotels", envelope.RootElement.GetProperty("payload").GetProperty("name").GetString());
    }

    [Fact]
    public void Start_WithInvalidDispatcherSettings_FailsBeforeServingRequests()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Eventing:Dispatcher:BatchSize", "0"));

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }

    private sealed class FixedTenant : ICurrentTenant
    {
        public TenantId TenantId => tenantId;
    }
}
