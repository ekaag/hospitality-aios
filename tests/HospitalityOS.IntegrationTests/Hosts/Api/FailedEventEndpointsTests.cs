using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.FailedEvents;
using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Organizations;
using HospitalityOS.Eventing;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The failed-event view (L1, spec §42: failed event processing is visible): an administrator sees their own tenant's
/// events that could not be published or that a handler kept failing on, and processes them again once the cause is fixed.
/// </summary>
public sealed class FailedEventEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/failed-events";
    private const string Handler = "Test.SyncOrganizationToCrm";
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(15);

    private readonly CrmProbe crm = new();
    private readonly StaffApi api;
    private readonly TenantId tenant = new(Guid.CreateVersion7());
    private readonly TenantId otherTenant = new(Guid.CreateVersion7());

    public FailedEventEndpointsTests()
    {
        api = new StaffApi(dispatchEvents: true, configure: builder =>
        {
            builder.UseSetting("Bindings:EventTransport:Default:Settings:RedeliveryDelay", "00:00:00");
            builder.ConfigureTestServices(services => services
                .AddSingleton(crm)
                .AddEventHandler<OrganizationCreatedV1, SyncOrganizationToCrm>(Handler));
        });
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AHandlersFailure_IsListedForItsTenantOnly_AndRetriedOnceFixed_HandlingItOnce()
    {
        using HttpClient admin = api.Staff(tenant);
        using HttpClient otherAdmin = api.Staff(otherTenant);
        await api.CreatePropertyAsync(tenant, Cancellation);
        await api.CreatePropertyAsync(otherTenant, Cancellation);

        FailedEventResponse failed = await FailedAsync(admin);
        Assert.Equal(
            ("OrganizationCreated", FailedEventStage.Handle, Handler, "Organization"),
            (failed.EventType, failed.Stage, failed.Handler, failed.AggregateType));
        Assert.Contains("The CRM is down", failed.Error, StringComparison.Ordinal);
        Assert.True(failed.Attempts > 1, $"{failed.Attempts} attempts");
        Assert.All(await ListAsync(otherAdmin), other => Assert.NotEqual(failed.EventId, other.EventId));

        crm.Up = true;
        using HttpResponseMessage retried = await admin.PostAsJsonAsync($"{Route}/{failed.EventId}/retry", new RetryFailedEventRequest(Handler), Cancellation);

        Assert.Equal(
            new FailedEventRetriedResponse(failed.EventId, FailedEventStage.Handle, Handler),
            await StaffApi.OkAsync<FailedEventRetriedResponse>(retried, Cancellation));
        await SyncedAsync(tenant);
        Assert.DoesNotContain(await ListAsync(admin), listed => listed.EventId == failed.EventId);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), record => record.Action == "event.retry" && record.TargetId == failed.EventId.ToString());
    }

    [Fact]
    public async Task AnEventThatCouldNotBePublished_IsListed_AndRequeuedToBePublishedAgain()
    {
        // Without the dispatcher the tenant's events stay in the outbox, as they do while the broker refuses them.
        await using StaffApi quiet = new();
        await quiet.CreatePropertyAsync(tenant, Cancellation);
        EventMessage recorded = (await quiet.EventsAsync(tenant, Cancellation))[0];
        await using (AsyncServiceScope scope = quiet.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IOutboxStore>().DeadLetterAsync(recorded.EventId, StaffApi.Now, "TimeoutException: the broker did not answer", Cancellation);
        }

        using HttpClient admin = quiet.Staff(tenant);
        FailedEventResponse failed = Assert.Single(await ListAsync(admin));
        using HttpResponseMessage retried = await admin.PostAsJsonAsync($"{Route}/{recorded.EventId}/retry", new RetryFailedEventRequest(), Cancellation);

        Assert.Equal(
            (recorded.EventId, FailedEventStage.Publish, (string?)null, "TimeoutException: the broker did not answer", StaffApi.Now),
            (failed.EventId, failed.Stage, failed.Handler, failed.Error, failed.FailedAt));
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Empty(await ListAsync(admin));
        await using AsyncServiceScope check = quiet.Factory.Services.CreateAsyncScope();
        IReadOnlyList<OutboxEntry> due = await check.ServiceProvider.GetRequiredService<IOutboxStore>().ClaimDueAsync(StaffApi.Now.AddSeconds(1), 100, TimeSpan.FromMinutes(1), Cancellation);
        Assert.Contains(due, entry => entry.Message.EventId == recorded.EventId);
    }

    [Fact]
    public async Task OnlyAdministrators_SeeOrRetryFailedEvents_AndAFailureThatIsNotThereIsNotFound()
    {
        using HttpClient manager = api.Staff(tenant, "Manager");
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage list = await manager.GetAsync(Route, Cancellation);
        using HttpResponseMessage retry = await manager.PostAsJsonAsync($"{Route}/{Guid.CreateVersion7()}/retry", new RetryFailedEventRequest(), Cancellation);
        using HttpResponseMessage missing = await admin.PostAsJsonAsync($"{Route}/{Guid.CreateVersion7()}/retry", new RetryFailedEventRequest(), Cancellation);
        using HttpResponseMessage unknownHandler = await admin.PostAsJsonAsync($"{Route}/{Guid.CreateVersion7()}/retry", new RetryFailedEventRequest("Test.NoSuchHandler"), Cancellation);

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (list.StatusCode, retry.StatusCode));
        Assert.Equal((HttpStatusCode.NotFound, "failed_event.not_found"), await StaffApi.ProblemAsync(missing, Cancellation));
        Assert.Equal((HttpStatusCode.NotFound, "failed_event.not_found"), await StaffApi.ProblemAsync(unknownHandler, Cancellation));
    }

    public ValueTask DisposeAsync() => api.DisposeAsync();

    /// <summary>
    /// Waits, moving the host's clock as the dispatcher needs, until the handler's failure is listed.
    /// </summary>
    private async Task<FailedEventResponse> FailedAsync(HttpClient client)
    {
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        limit.CancelAfter(timeout);

        while (true)
        {
            if ((await ListAsync(client)).FirstOrDefault(failed => failed.Handler == Handler) is { } failed)
            {
                return failed;
            }

            api.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(50, limit.Token);
        }
    }

    /// <summary>
    /// Waits, moving the host's clock as the dispatcher needs, until the CRM has the tenant's organization.
    /// </summary>
    private async Task SyncedAsync(TenantId tenantId)
    {
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        limit.CancelAfter(timeout);
        Task synced = crm.SyncedAsync(tenantId);

        while (!synced.IsCompleted)
        {
            api.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(50, limit.Token);
        }
    }

    private static async Task<IReadOnlyList<FailedEventResponse>> ListAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync(new Uri(Route, UriKind.Relative), Cancellation);

        return await StaffApi.OkAsync<FailedEventResponse[]>(response, Cancellation);
    }

    /// <summary>
    /// A downstream system, down until a test brings it up.
    /// </summary>
    private sealed class CrmProbe
    {
        private readonly Dictionary<TenantId, TaskCompletionSource> synced = [];

        public bool Up { get; set; }

        public Task SyncedAsync(TenantId tenantId) => Completion(tenantId).Task;

        public void Synced(TenantId tenantId) => Completion(tenantId).TrySetResult();

        private TaskCompletionSource Completion(TenantId tenantId)
        {
            lock (synced)
            {
                return synced.TryGetValue(tenantId, out TaskCompletionSource? completion)
                    ? completion
                    : synced[tenantId] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    private sealed class SyncOrganizationToCrm(CrmProbe crm) : IEventHandler<OrganizationCreatedV1>
    {
        public Task HandleAsync(EventEnvelope envelope, OrganizationCreatedV1 payload, CancellationToken cancellationToken)
        {
            if (!crm.Up)
            {
                throw new InvalidOperationException("The CRM is down.");
            }

            crm.Synced(envelope.TenantId);

            return Task.CompletedTask;
        }
    }
}
