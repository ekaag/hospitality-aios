using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Capabilities.GuestService;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Contracts.Events.Operations;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Eventing.Replay;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.Workflows.GuestService;
using HospitalityOS.Workflows.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Workflows;

/// <summary>
/// The Guest Service Controller resuming on staff work, end to end with events dispatched against the seeded Aurora Grand
/// (Alex in room 412; Jo Okafor first in line for housekeeping): work started puts the request in progress and completed
/// completes it, once however often completion is delivered (Journeys 14, 19), every step in the request's flow (P11);
/// the status check reports where the work is (Journey 12); failed or expired work keeps the request unresolved in
/// exception (Journey 20); and a request with more work open waits for the last piece.
/// </summary>
public sealed class RequestContinuationTests : IAsyncLifetime
{
    private static readonly Actor agent = Actor.Agent("guestservice.extract");
    private static readonly Actor jo = Actor.Staff(Seed.JoOkafor.ToString(), StaffRole.Worker);

    private readonly StaffApi api = new(seedAuroraGrand: true, dispatchEvents: true);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// Waits until the seeded roster's joining has been routed, so it cannot race the test's own work.
    /// </summary>
    public async ValueTask InitializeAsync() =>
        await api.HandledAsync(Seed.TenantId, "StaffMemberCreated", DutyRerouting.JoinedName, Cancellation);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task WorkStartedThenCompleted_MovesTheRequestOn_InTheRequestsFlow_WithTheStatusCheckFollowingTheWork()
    {
        (ServiceRequestId request, Guid flow) = await TowelsAsync();
        HumanAction work = await WorkForAsync(request, action => action.AssignedTo == Seed.JoOkafor);
        ServiceRequestEntry assigned = await StatusAsync();

        await ActAsync(jo, "HumanAction.UpdateStatus", new UpdateActionStatusInput(work.Id.Value, ActionStatus.Accepted), work);
        await ActAsync(jo, "HumanAction.UpdateStatus", new UpdateActionStatusInput(work.Id.Value, ActionStatus.InProgress), work);
        await UntilAsync(request, found => found.Status == ServiceRequestStatus.InProgress);
        ServiceRequestEntry started = await StatusAsync();
        await ActAsync(jo, "HumanAction.Complete", new CompleteActionInput(work.Id.Value, "Left at the door"), work);
        ServiceRequest completed = await UntilAsync(request, found => found.Status == ServiceRequestStatus.Completed);

        Assert.Equal((RequestStatus.Open, (ActionStatus?)ActionStatus.Assigned), (assigned.Status, assigned.WorkStatus));
        Assert.Equal((RequestStatus.InProgress, (ActionStatus?)ActionStatus.InProgress), (started.Status, started.WorkStatus));
        Assert.NotNull(completed.CompletedAt);
        Assert.Empty((await StatusOutputAsync()).Requests);

        IReadOnlyList<EventMessage> events = await EventsUntilAsync("ServiceRequestCompleted");
        string[] journey =
        [
            "ServiceRequestCreated", "HumanActionRequested", "HumanActionAssigned", "HumanActionAccepted", "HumanActionStarted",
            "ServiceRequestUpdated", "HumanActionCompleted", "ServiceRequestCompleted",
        ];
        Assert.All(journey, type => Assert.Equal(flow, Assert.Single(events, message => message.EventType == type).CorrelationId));
    }

    [Fact]
    public async Task ACompletionDeliveredOrMadeAgain_CompletesTheRequestOnce()
    {
        (ServiceRequestId request, _) = await TowelsAsync();
        HumanAction work = await WorkForAsync(request, action => action.AssignedTo == Seed.JoOkafor);
        await ActAsync(jo, "HumanAction.UpdateStatus", new UpdateActionStatusInput(work.Id.Value, ActionStatus.Accepted), work);
        await ActAsync(jo, "HumanAction.Complete", new CompleteActionInput(work.Id.Value), work);
        await ActAsync(jo, "HumanAction.Complete", new CompleteActionInput(work.Id.Value, "Tapped twice"), work);
        await UntilAsync(request, found => found.Status == ServiceRequestStatus.Completed);

        await using (AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IEventReplayer>().ReplayAsync(new EventReplayRequest(TenantId: Seed.TenantId), Cancellation) > 0);
        }

        // A later request is handled after the replayed deliveries, which by then were skipped as already processed.
        (ServiceRequestId later, _) = await TowelsAsync("request-2");
        await WorkForAsync(later, action => action.AssignedTo is not null);

        IReadOnlyList<EventMessage> events = await EventsUntilAsync("ServiceRequestCompleted");
        Assert.Single(events, message => message.EventType == "HumanActionCompleted");
        Assert.Single(events, message => message.EventType == "ServiceRequestCompleted");
    }

    [Fact]
    public async Task FailedWork_KeepsTheRequestUnresolved_InExceptionWithTheReason()
    {
        (ServiceRequestId request, _) = await TowelsAsync();
        HumanAction work = await WorkForAsync(request, action => action.AssignedTo == Seed.JoOkafor);
        await ActAsync(jo, "HumanAction.UpdateStatus", new UpdateActionStatusInput(work.Id.Value, ActionStatus.Accepted), work);

        await ActAsync(jo, "HumanAction.UpdateStatus", new UpdateActionStatusInput(work.Id.Value, ActionStatus.Failed, "Could not access the room"), work);

        ServiceRequest failed = await UntilAsync(request, found => found.Status == ServiceRequestStatus.Exception);
        Assert.Equal(("The work failed: Could not access the room", true), (failed.ExceptionReason, failed.IsActive));
        ServiceRequestEntry status = await StatusAsync();
        Assert.Equal((RequestStatus.Exception, (ActionStatus?)ActionStatus.Failed), (status.Status, status.WorkStatus));
        Assert.DoesNotContain(await api.EventsAsync(Seed.TenantId, Cancellation), message => message.EventType == "ServiceRequestCompleted");
    }

    [Fact]
    public async Task ARequestWithMoreWorkOpen_IsCompletedByTheLastPiece_AndNewWorkTakesItOutOfException()
    {
        (ServiceRequestId request, _) = await TowelsAsync();
        HumanAction first = await WorkForAsync(request, action => action.AssignedTo == Seed.JoOkafor);
        await ActAsync(jo, "HumanAction.UpdateStatus", new UpdateActionStatusInput(first.Id.Value, ActionStatus.Accepted), first);
        await ActAsync(jo, "HumanAction.UpdateStatus", new UpdateActionStatusInput(first.Id.Value, ActionStatus.Failed, "Ran out of towels on the floor"), first);
        await UntilAsync(request, found => found.Status == ServiceRequestStatus.Exception);

        // A manager sends two more pieces of work for the same request, which the router shares between the housekeepers.
        Actor manager = Actor.Staff("maria", StaffRole.Manager);
        HumanActionEntry second = await ActAsync(manager, "HumanAction.Create", Retry(request), null, "retry-1");
        HumanActionEntry third = await ActAsync(manager, "HumanAction.Create", Retry(request), null, "retry-2");
        HumanAction secondWork = await LoadAsync(second.Source.Id, action => action.AssignedTo is not null);
        HumanAction thirdWork = await LoadAsync(third.Source.Id, action => action.AssignedTo is not null);

        await CompleteAsync(secondWork);
        await EventsUntilAsync("HumanActionCompleted");
        await api.HandledAsync(Seed.TenantId, "HumanActionCompleted", RequestContinuation.OnCompleted, Cancellation);
        Assert.Equal(ServiceRequestStatus.InProgress, (await LoadRequestAsync(request)).Status);
        await CompleteAsync(thirdWork);

        await UntilAsync(request, found => found.Status == ServiceRequestStatus.Completed);
        Assert.Single(await EventsUntilAsync("ServiceRequestCompleted"), message => message.EventType == "ServiceRequestCompleted");
    }

    [Fact]
    public async Task ExpiredWork_PutsTheRequestIntoException()
    {
        (ServiceRequestId request, _) = await TowelsAsync();
        HumanAction work = await WorkForAsync(request, action => action.AssignedTo is not null);

        await using (AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);
            await scope.ServiceProvider.GetRequiredService<RequestContinuation>()
                .HandleAsync(null!, new HumanActionExpiredV1(work.Id.Value, request), Cancellation);
            Assert.True((await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation)).IsSuccess);
        }

        ServiceRequest expired = await LoadRequestAsync(request);
        Assert.Equal((ServiceRequestStatus.Exception, "The work was not started before its deadline."), (expired.Status, expired.ExceptionReason));
    }

    private static CreateActionInput Retry(ServiceRequestId request) => new(
        Seed.PropertyId,
        ActionType.DeliverItems,
        "Deliver 1 × Bath towel",
        new ActionLocation("Room 412", Seed.Room412, Seed.Room412Space),
        "The first delivery failed.",
        Items: [new ActionItemEntry("Bath towel", 1, "bath_towel")],
        SkillsRequired: ["housekeeping"],
        ServiceRequestId: request);

    private async Task CompleteAsync(HumanAction work)
    {
        Actor assignee = Actor.Staff(work.AssignedTo!.Value.ToString(), StaffRole.Worker);
        await ActAsync(assignee, "HumanAction.UpdateStatus", new UpdateActionStatusInput(work.Id.Value, ActionStatus.Accepted), work);
        await ActAsync(assignee, "HumanAction.UpdateStatus", new UpdateActionStatusInput(work.Id.Value, ActionStatus.InProgress), work);
        await ActAsync(assignee, "HumanAction.Complete", new CompleteActionInput(work.Id.Value), work);
    }

    /// <summary>
    /// Alex's request for two towels, made as the controller makes it, and the flow it starts.
    /// </summary>
    private async Task<(ServiceRequestId Request, Guid Flow)> TowelsAsync(string key = "request-1")
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);
        OperationContext operation = scope.ServiceProvider.GetRequiredService<OperationContext>();
        operation.ActAs(agent);
        Result<CapabilityResponse> created = await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(
            new CapabilityRequest(
                "GuestService.CreateRequest",
                JsonSerializer.SerializeToElement(new CreateRequestInput(Seed.AlexStay, "housekeeping.amenities", "bath_towel", 2), ContractJson.Options),
                IdempotencyKey: key),
            Cancellation);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

        return (new ServiceRequestId(created.Value.Output.Deserialize<CreateRequestOutput>(ContractJson.Options)!.Request.Source.Id), operation.CorrelationId);
    }

    /// <summary>
    /// Acts on work as <paramref name="actor"/> would from the staff surface: continuing the work's flow.
    /// </summary>
    private async Task<HumanActionEntry> ActAsync(Actor actor, string capability, object input, HumanAction? work, string? key = null)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(actor);
        Result<CapabilityResponse> result = await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(
            new CapabilityRequest(capability, JsonSerializer.SerializeToElement(input, ContractJson.Options), IdempotencyKey: key, ContinuesFlow: work?.Details.CorrelationId),
            Cancellation);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);

        return result.Value.Output.Deserialize<HumanActionOutput>(ContractJson.Options)!.Action;
    }

    private async Task<ServiceRequestEntry> StatusAsync() => Assert.Single((await StatusOutputAsync()).Requests);

    private Task<GetRequestStatusOutput> StatusOutputAsync() =>
        api.AskAsGuestAsync<GetRequestStatusOutput>(Seed.TenantId, "GuestService.GetRequestStatus", new GetRequestStatusInput(Seed.AlexStay), Cancellation);

    private async Task<ServiceRequest> LoadRequestAsync(ServiceRequestId id)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);

        return await scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>().GetByIdAsync(id, Cancellation)
            ?? throw new Xunit.Sdk.XunitException($"Service request {id} does not exist.");
    }

    private Task<ServiceRequest> UntilAsync(ServiceRequestId id, Func<ServiceRequest, bool> condition) =>
        PollAsync(() => LoadRequestAsync(id), condition);

    private Task<HumanAction> WorkForAsync(ServiceRequestId request, Func<HumanAction, bool> condition) =>
        PollAsync(
            async () =>
            {
                await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);
                IReadOnlyList<HumanAction> work = await scope.ServiceProvider.GetRequiredService<IHumanActionRepository>().ListForServiceRequestAsync(request, Cancellation);

                return work.Count == 0 ? null! : work[0];
            },
            action => action is not null && condition(action));

    private Task<HumanAction> LoadAsync(Guid id, Func<HumanAction, bool> condition) =>
        PollAsync(
            async () =>
            {
                await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);

                return (await scope.ServiceProvider.GetRequiredService<IHumanActionRepository>().GetByIdAsync(new HumanActionId(id), Cancellation))!;
            },
            action => action is not null && condition(action));

    /// <summary>
    /// The tenant's events once one of <paramref name="type"/> is among them: events reach the store as they are dispatched.
    /// </summary>
    private Task<IReadOnlyList<EventMessage>> EventsUntilAsync(string type) =>
        PollAsync(() => api.EventsAsync(Seed.TenantId, Cancellation), events => events.Any(message => message.EventType == type));

    private async Task<T> PollAsync<T>(Func<Task<T>> load, Func<T, bool> condition)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            T value = await load();

            if (condition(value))
            {
                return value;
            }

            api.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }
}
