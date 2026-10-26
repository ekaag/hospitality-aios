using System.Collections.Concurrent;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Organizations;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Eventing;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.Eventing.Replay;
using HospitalityOS.Eventing.Serialization;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Eventing;

/// <summary>
/// Idempotent handling in the real host (P8, Journey 19): a handler's effect happens once however often its event is
/// delivered, redelivered, replayed, or raced; failures are retried and then dead-lettered; and the handler's own events
/// continue the flow. Each subscription processes in order, so waiting for a later marker proves earlier events were handled.
/// </summary>
public sealed class EventHandlingTests : IAsyncDisposable
{
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(10);
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209900001"));

    private readonly ConcurrentDictionary<(string EventType, Guid AggregateId), TaskCompletionSource<EventMessage>> seen = new();
    private WebApplicationFactory<Program>? factory;
    private IAsyncDisposable? probeSubscription;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private HandlerProbe Probe => factory!.Services.GetRequiredService<HandlerProbe>();

    [Fact]
    public async Task Handler_CommittedEvent_TakesEffectOnceAndContinuesTheFlow()
    {
        await StartAsync();

        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");

        EventMessage created = await SeenAsync("OrganizationCreated", organization);
        EventEnvelope update = Read(await SeenAsync("OrganizationUpdated", organization));
        Assert.Equal("Aurora Hotels!", await NameOfAsync(organization));
        Assert.Equal((created.CorrelationId, (Guid?)created.EventId), (update.CorrelationId, update.CausationId));
        Assert.Equal((ActorType.Agent, ExclaimHandler.Name), (update.ActorType, update.ActorId));
    }

    [Fact]
    public async Task Handler_EventRedeliveredAndReplayed_TakesEffectOnce()
    {
        await StartAsync();
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        EventMessage created = await SeenAsync("OrganizationCreated", organization);
        await SeenAsync("OrganizationUpdated", organization);
        IEventTransport transport = Transport();

        await transport.PublishAsync(created, Cancellation);
        await transport.PublishAsync(created, Cancellation);
        int replayed = await factory!.Services.GetRequiredService<IEventReplayer>().ReplayAsync(new EventReplayRequest(), Cancellation);

        await SeenAsync("OrganizationUpdated", await CreateOrganizationAsync("Marker"));
        Assert.True(replayed >= 2);
        Assert.Equal("Aurora Hotels!", await NameOfAsync(organization));
        Assert.Equal(1, Probe.RunsFor(organization.Value));
    }

    [Fact]
    public async Task Handler_FailsTwiceThenSucceeds_IsRedeliveredAndTakesEffectOnce()
    {
        await StartAsync();
        OrganizationId organization = new(Guid.CreateVersion7());
        Probe.FailNext(organization.Value, times: 2);

        await CreateOrganizationAsync("Aurora Hotels", organization);

        await SeenAsync("OrganizationUpdated", organization);
        Assert.Equal("Aurora Hotels!", await NameOfAsync(organization));
        Assert.Equal(3, Probe.RunsFor(organization.Value));
    }

    [Fact]
    public async Task Handler_AlwaysFails_IsDeadLetteredWithoutBlockingLaterEvents()
    {
        await StartAsync();
        OrganizationId organization = new(Guid.CreateVersion7());
        Probe.FailNext(organization.Value, times: int.MaxValue);
        await CreateOrganizationAsync("Aurora Hotels", organization);

        await SeenAsync("OrganizationUpdated", await CreateOrganizationAsync("Marker"));

        DeadLetteredEvent deadLettered = Assert.Single(await Transport().ReadDeadLettersAsync(ExclaimHandler.Name, Cancellation));
        Assert.Equal(organization.Value, deadLettered.Message.AggregateId);
        Assert.Contains("Injected failure", deadLettered.Reason, StringComparison.Ordinal);
        Assert.Equal("Aurora Hotels", await NameOfAsync(organization));
    }

    [Fact]
    public async Task Process_TwoDeliveriesRacingPastTheInboxCheck_TakeEffectOnce()
    {
        await StartAsync(dispatcherEnabled: false);
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        EventMessage created = await RecordedAsync("OrganizationCreated");
        EventProcessor processor = factory!.Services.GetRequiredService<EventProcessor>();
        EventHandlerRegistration registration = factory.Services.GetServices<EventHandlerRegistration>().Single(candidate => candidate.Name == ExclaimHandler.Name);
        Probe.HoldUntil(2);

        HandlingOutcome[] outcomes = await Task.WhenAll(
            Task.Run(() => processor.ProcessAsync(registration, created, Cancellation), Cancellation),
            Task.Run(() => processor.ProcessAsync(registration, created, Cancellation), Cancellation));

        Assert.Equal([HandlingOutcome.Handled, HandlingOutcome.Duplicate], outcomes.Order());
        Assert.Equal("Aurora Hotels!", await NameOfAsync(organization));
    }

    public async ValueTask DisposeAsync()
    {
        if (probeSubscription is not null)
        {
            await probeSubscription.DisposeAsync();
        }

        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
    }

    private async Task StartAsync(bool dispatcherEnabled = true)
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Eventing:Dispatcher:Enabled", dispatcherEnabled ? "true" : "false");
            builder.UseSetting("Eventing:Dispatcher:PollInterval", "00:00:00.050");
            builder.UseSetting("Bindings:EventTransport:Default:Settings:RedeliveryDelay", "00:00:00");
            builder.ConfigureTestServices(services => services
                .AddSingleton<HandlerProbe>()
                .AddEventHandler<OrganizationCreatedV1, ExclaimHandler>(ExclaimHandler.Name));
        });

        probeSubscription = await Transport().SubscribeAsync(
            "test-observer",
            (message, _) =>
            {
                seen.GetOrAdd((message.EventType, message.AggregateId), _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult(message);

                return Task.CompletedTask;
            },
            Cancellation);
    }

    private IEventTransport Transport() => factory!.Services.GetRequiredService<IBindingResolver<IEventTransport>>().Resolve();

    private Task<EventMessage> SeenAsync(string eventType, OrganizationId organization) =>
        seen.GetOrAdd((eventType, organization.Value), _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task.WaitAsync(timeout, Cancellation);

    private EventEnvelope Read(EventMessage message) => factory!.Services.GetRequiredService<EventSerializer>().Deserialize(message.Envelope);

    private async Task<OrganizationId> CreateOrganizationAsync(string name, OrganizationId? id = null)
    {
        OrganizationId organizationId = id ?? new OrganizationId(Guid.CreateVersion7());
        await using AsyncServiceScope scope = factory!.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        Organization organization = Organization.Create(organizationId, tenantId, name, DateTimeOffset.UtcNow).Value;
        await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>().AddAsync(organization, Cancellation);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation)).IsSuccess);

        return organizationId;
    }

    private async Task<string?> NameOfAsync(OrganizationId organization)
    {
        await using AsyncServiceScope scope = factory!.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);

        return (await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>().GetByIdAsync(organization, Cancellation))?.Name;
    }

    private async Task<EventMessage> RecordedAsync(string eventType)
    {
        await using AsyncServiceScope scope = factory!.Services.CreateAsyncScope();
        IReadOnlyList<StoredEvent> recorded = await scope.ServiceProvider.GetRequiredService<IEventStore>().ReadAsync(new EventStoreQuery(), Cancellation);

        return recorded.Single(stored => stored.Message.EventType == eventType).Message;
    }
}
