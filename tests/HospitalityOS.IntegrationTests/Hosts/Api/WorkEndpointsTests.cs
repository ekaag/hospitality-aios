using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Api.Work;
using HospitalityOS.Contracts.Capabilities.GuestService;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.Workflows.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The staff surface's API (J2, spec §32) against the seeded Aurora Grand: a worker sees and steps through their own work
/// (Accept, then Start, then Complete or Fail) in the work's own flow (Journeys 13, 14); unassigned work is claimable by
/// those eligible for it and assignable by managers; failed work whose request is still waiting shows as an exception that
/// managers can ask for again (Journey 20); and the view's buttons are the steps the caller may take.
/// </summary>
public sealed class WorkEndpointsTests : IAsyncLifetime
{
    private static readonly Actor agent = Actor.Agent("guestservice.extract");
    private static readonly string[] workerJourney = ["HumanActionAccepted", "HumanActionStarted", "HumanActionCompleted", "ServiceRequestCompleted"];

    private readonly StaffApi api = new(seedAuroraGrand: true, dispatchEvents: true);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// Waits until the seeded roster's joining has been routed, so it cannot race the test's own work.
    /// </summary>
    public async ValueTask InitializeAsync() =>
        await api.HandledAsync(Seed.TenantId, "StaffMemberCreated", DutyRerouting.JoinedName, Cancellation);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AWorker_StepsThroughTheirWork_InItsFlow_AndOnlyTheyCanSeeIt()
    {
        (ServiceRequestId request, Guid flow) = await TowelsAsync();
        using HttpClient jo = Worker(Seed.JoOkafor);
        using HttpClient sam = Worker(Seed.SamPatel);

        // Both housekeepers are free, so the router takes them by name.
        WorkItemResponse assigned = await UntilAsync(jo, WorkView.Assigned, items => items.Count == 1);
        Assert.Equal(
            ("Deliver 2 × Bath towel", "Room 412", ActionStatus.Assigned, "Jo Okafor"),
            (assigned.Instructions, assigned.Location, assigned.Status, assigned.AssigneeName));
        Assert.Equal([WorkAction.Accept], assigned.Actions);
        Assert.Equal([new ActionItemEntry("Bath towel", 2, "bath_towel")], assigned.Items);
        Assert.NotNull(assigned.Deadline);
        Assert.Empty(await ListAsync(sam, WorkView.Assigned));

        WorkItemResponse accepted = await StepAsync(jo, assigned, "accept");
        WorkItemResponse started = await StepAsync(jo, assigned, "start");
        WorkItemResponse completed = await StepAsync(jo, assigned, "complete", new CompleteWorkRequest("Left at the door"));

        Assert.Equal(ActionStatus.Accepted, accepted.Status);
        Assert.Equal([WorkAction.Start, WorkAction.Fail], accepted.Actions);
        Assert.Equal(ActionStatus.InProgress, started.Status);
        Assert.Equal([WorkAction.Complete, WorkAction.Fail], started.Actions);
        Assert.Equal((ActionStatus.Completed, "Left at the door", 0), (completed.Status, completed.CompletionNote, completed.Actions.Count));
        Assert.Empty(await ListAsync(jo, WorkView.Assigned));
        Assert.Equal([assigned.Id], (await ListAsync(jo, WorkView.Completed)).Select(item => item.Id));
        Assert.Empty(await ListAsync(sam, WorkView.Completed));

        // The worker's steps continue the request's flow, which the guest service then completes.
        IReadOnlyList<EventMessage> events = await EventsUntilAsync("ServiceRequestCompleted");
        Assert.All(
            workerJourney,
            type => Assert.Equal(flow, Assert.Single(events, message => message.EventType == type).CorrelationId));
        Assert.Equal(ServiceRequestStatus.Completed, (await LoadRequestAsync(request)).Status);
    }

    [Fact]
    public async Task AStepOutOfTurn_OrOnSomeoneElsesWork_IsRefused_AndARepeatedStepChangesNothing()
    {
        await TowelsAsync();
        using HttpClient jo = Worker(Seed.JoOkafor);
        using HttpClient sam = Worker(Seed.SamPatel);
        WorkItemResponse work = await UntilAsync(jo, WorkView.Assigned, items => items.Count == 1);

        using HttpResponseMessage early = await jo.PostAsync($"/api/v1/work/{work.Id}/start", null, Cancellation);
        using HttpResponseMessage notTheirs = await sam.PostAsync($"/api/v1/work/{work.Id}/accept", null, Cancellation);
        await StepAsync(jo, work, "accept");
        WorkItemResponse again = await StepAsync(jo, work, "accept");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, early.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notTheirs.StatusCode);
        Assert.Equal(ActionStatus.Accepted, again.Status);
        Assert.Single(await api.EventsAsync(Seed.TenantId, Cancellation), message => message.EventType == "HumanActionAccepted");
    }

    [Fact]
    public async Task UnassignedWork_IsClaimedByAnEligibleWorker_AndOnlyTheyAreOfferedIt()
    {
        await using StaffApi quiet = new(seedAuroraGrand: true);
        Guid work = await UnroutedWorkAsync(quiet);
        using HttpClient sam = quiet.Staff(Seed.TenantId, "Worker", Seed.SamPatel.ToString());
        using HttpClient ravi = quiet.Staff(Seed.TenantId, "Worker", Seed.RaviMehta.ToString());
        using HttpClient manager = quiet.Staff(Seed.TenantId, "Manager");

        WorkItemResponse offered = Assert.Single(await ListAsync(sam, WorkView.Unassigned));
        WorkItemResponse overseen = Assert.Single(await ListAsync(manager, WorkView.Unassigned));
        Assert.Equal(work, offered.Id);
        Assert.Equal([WorkAction.Claim], offered.Actions);
        Assert.Equal([WorkAction.Assign], overseen.Actions);
        Assert.Empty(await ListAsync(ravi, WorkView.Unassigned));

        using HttpResponseMessage refused = await ravi.PostAsync($"/api/v1/work/{work}/claim", null, Cancellation);
        WorkItemResponse claimed = await StepAsync(sam, offered, "claim");
        WorkItemResponse again = await StepAsync(sam, offered, "claim");

        Assert.Equal((HttpStatusCode.Forbidden, "human_action.not_eligible"), await StaffApi.ProblemAsync(refused, Cancellation));
        Assert.Equal((ActionStatus.Accepted, (StaffMemberId?)Seed.SamPatel), (claimed.Status, claimed.AssignedTo));
        Assert.Equal([WorkAction.Start, WorkAction.Fail], claimed.Actions);
        Assert.Equal(ActionStatus.Accepted, again.Status);
        Assert.Empty(await ListAsync(sam, WorkView.Unassigned));
        Assert.Equal([work], (await ListAsync(sam, WorkView.Assigned)).Select(item => item.Id));
    }

    [Fact]
    public async Task AManager_AssignsUnassignedWork_AndAWorkerCannot()
    {
        await using StaffApi quiet = new(seedAuroraGrand: true);
        Guid work = await UnroutedWorkAsync(quiet);
        using HttpClient manager = quiet.Staff(Seed.TenantId, "Manager");
        using HttpClient sam = quiet.Staff(Seed.TenantId, "Worker", Seed.SamPatel.ToString());
        using HttpClient jo = quiet.Staff(Seed.TenantId, "Worker", Seed.JoOkafor.ToString());

        using HttpResponseMessage byWorker = await sam.PostAsJsonAsync($"/api/v1/work/{work}/assign", new AssignWorkRequest(Seed.SamPatel), ContractJson.Options, Cancellation);
        using HttpResponseMessage assigned = await manager.PostAsJsonAsync($"/api/v1/work/{work}/assign", new AssignWorkRequest(Seed.JoOkafor), ContractJson.Options, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, byWorker.StatusCode);
        WorkItemResponse item = await StaffApi.OkAsync<WorkItemResponse>(assigned, Cancellation);
        Assert.Equal((ActionStatus.Assigned, (StaffMemberId?)Seed.JoOkafor, "Jo Okafor"), (item.Status, item.AssignedTo, item.AssigneeName));
        Assert.Equal([WorkAction.Accept], Assert.Single(await ListAsync(jo, WorkView.Assigned)).Actions);
    }

    [Fact]
    public async Task FailedWork_IsAnExceptionUntilAManagerAsksForItAgain_Once()
    {
        (ServiceRequestId request, Guid flow) = await TowelsAsync();
        using HttpClient jo = Worker(Seed.JoOkafor);
        using HttpClient sam = Worker(Seed.SamPatel);
        using HttpClient manager = api.Staff(Seed.TenantId, "Manager");
        WorkItemResponse work = await UntilAsync(jo, WorkView.Assigned, items => items.Count == 1);
        await StepAsync(jo, work, "accept");

        using HttpResponseMessage noReason = await jo.PostAsJsonAsync($"/api/v1/work/{work.Id}/fail", new FailWorkRequest(), ContractJson.Options, Cancellation);
        await StepAsync(jo, work, "fail", new FailWorkRequest("Could not access the room"));

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        WorkItemResponse exception = await UntilAsync(manager, WorkView.Exceptions, items => items.Count == 1);
        Assert.Equal((work.Id, "Could not access the room"), (exception.Id, exception.FailureReason));
        Assert.Equal([WorkAction.RequestAgain], exception.Actions);
        Assert.Empty(Assert.Single(await ListAsync(jo, WorkView.Exceptions)).Actions);
        Assert.Empty(await ListAsync(sam, WorkView.Exceptions));

        using HttpResponseMessage byWorker = await jo.PostAsync($"/api/v1/work/{work.Id}/request-again", null, Cancellation);
        WorkItemResponse again = await StaffApi.CreatedAsync<WorkItemResponse>(await manager.PostAsync($"/api/v1/work/{work.Id}/request-again", null, Cancellation), Cancellation);
        using HttpResponseMessage twice = await manager.PostAsync($"/api/v1/work/{work.Id}/request-again", null, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, byWorker.StatusCode);
        Assert.NotEqual(work.Id, again.Id);
        Assert.Equal(("Deliver 2 × Bath towel", "Room 412"), (again.Instructions, again.Location));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "work.not_an_exception"), await StaffApi.ProblemAsync(twice, Cancellation));
        Assert.Empty(await ListAsync(manager, WorkView.Exceptions));

        // The new work is routed and belongs to the request's flow.
        await UntilAsync(manager, WorkView.Assigned, items => items.Any(item => item.Id == again.Id));
        IReadOnlyList<EventMessage> events = await api.EventsAsync(Seed.TenantId, Cancellation);
        Assert.Equal(flow, Assert.Single(events, message => message.EventType == "HumanActionRequested" && message.AggregateId == again.Id).CorrelationId);
        Assert.Equal(ServiceRequestStatus.Exception, (await LoadRequestAsync(request)).Status);
    }

    [Fact]
    public async Task OnlyStaffOnThePropertysRoster_OrItsManagers_SeeItsWork()
    {
        using HttpClient stranger = api.Staff(Seed.TenantId, "Worker", Guid.NewGuid().ToString());
        using HttpClient guest = api.Guest(Seed.TenantId, new GuestStay(Seed.Alex, Seed.AlexStay, Seed.PropertyId));
        using HttpClient anonymous = api.Client();
        string uri = $"/api/v1/work?propertyId={Seed.PropertyId}&view=Assigned";

        using HttpResponseMessage notOnRoster = await stranger.GetAsync(uri, Cancellation);
        using HttpResponseMessage byGuest = await guest.GetAsync(uri, Cancellation);
        using HttpResponseMessage unauthenticated = await anonymous.GetAsync(uri, Cancellation);
        using HttpResponseMessage noView = await api.Staff(Seed.TenantId, "Manager").GetAsync($"/api/v1/work?propertyId={Seed.PropertyId}", Cancellation);

        Assert.Equal((HttpStatusCode.Forbidden, "work.not_on_roster"), await StaffApi.ProblemAsync(notOnRoster, Cancellation));
        Assert.Equal(HttpStatusCode.Forbidden, byGuest.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noView.StatusCode);
    }

    private HttpClient Worker(StaffMemberId id) => api.Staff(Seed.TenantId, "Worker", id.ToString());

    private static async Task<IReadOnlyList<WorkItemResponse>> ListAsync(HttpClient client, WorkView view) =>
        await StaffApi.OkAsync<IReadOnlyList<WorkItemResponse>>(
            await client.GetAsync($"/api/v1/work?propertyId={Seed.PropertyId}&view={view}", Cancellation), Cancellation);

    private static async Task<WorkItemResponse> StepAsync(HttpClient client, WorkItemResponse work, string step, object? body = null)
    {
        using HttpResponseMessage response = body is null
            ? await client.PostAsync($"/api/v1/work/{work.Id}/{step}", null, Cancellation)
            : await client.PostAsJsonAsync($"/api/v1/work/{work.Id}/{step}", body, ContractJson.Options, Cancellation);

        return await StaffApi.OkAsync<WorkItemResponse>(response, Cancellation);
    }

    /// <summary>
    /// The view's first item once <paramref name="done"/> holds, as routing and continuation catch up.
    /// </summary>
    private async Task<WorkItemResponse> UntilAsync(HttpClient client, WorkView view, Func<IReadOnlyList<WorkItemResponse>, bool> done)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            IReadOnlyList<WorkItemResponse> items = await ListAsync(client, view);

            if (done(items))
            {
                return items[0];
            }

            api.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    private async Task<IReadOnlyList<EventMessage>> EventsUntilAsync(string type)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            IReadOnlyList<EventMessage> events = await api.EventsAsync(Seed.TenantId, Cancellation);

            if (events.Any(message => message.EventType == type))
            {
                return events;
            }

            api.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    /// <summary>
    /// Alex's two towels, as the Guest Service Controller asks for them, and the flow they start.
    /// </summary>
    private async Task<(ServiceRequestId Request, Guid Flow)> TowelsAsync()
    {
        (CapabilityResponse created, Guid flow) = await AsAgentAsync(
            api, "GuestService.CreateRequest", new CreateRequestInput(Seed.AlexStay, "housekeeping.amenities", "bath_towel", 2), "request-1");

        return (new ServiceRequestId(created.Output.Deserialize<CreateRequestOutput>(ContractJson.Options)!.Request.Source.Id), flow);
    }

    /// <summary>
    /// Housekeeping work on a host that does not dispatch events, so the router never assigns it.
    /// </summary>
    private static async Task<Guid> UnroutedWorkAsync(StaffApi host)
    {
        (CapabilityResponse created, _) = await AsAgentAsync(
            host,
            "HumanAction.Create",
            new CreateActionInput(
                Seed.PropertyId,
                ActionType.DeliverItems,
                "Deliver 1 × Pillow",
                new ActionLocation("Room 412", Seed.Room412),
                "Guest asked for a pillow",
                SlaMinutes: 30,
                Items: [new ActionItemEntry("Pillow", 1, "pillow")],
                SkillsRequired: ["housekeeping"]),
            "work-1");

        return created.Output.Deserialize<HumanActionOutput>(ContractJson.Options)!.Action.Source.Id;
    }

    private static async Task<(CapabilityResponse Response, Guid Flow)> AsAgentAsync(StaffApi host, string capability, object input, string key)
    {
        await using AsyncServiceScope scope = host.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);
        OperationContext operation = scope.ServiceProvider.GetRequiredService<OperationContext>();
        operation.ActAs(agent);
        Result<CapabilityResponse> result = await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(
            new CapabilityRequest(capability, JsonSerializer.SerializeToElement(input, ContractJson.Options), IdempotencyKey: key),
            Cancellation);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);

        return (result.Value, operation.CorrelationId);
    }

    private async Task<ServiceRequest> LoadRequestAsync(ServiceRequestId id)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);

        return (await scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>().GetByIdAsync(id, Cancellation))!;
    }
}
