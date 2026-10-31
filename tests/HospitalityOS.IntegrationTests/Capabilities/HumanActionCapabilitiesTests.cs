using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Capabilities.GuestService;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Capabilities;

/// <summary>
/// The spec §11 human action capabilities through the executor in the real host, against the seeded Aurora Grand: an agent
/// asks for work, which raises <c>HumanActionRequested</c> once per key (P8); it is assigned and reassigned; and only its
/// assignee accepts, starts, completes, or fails it, each transition raising its own event (Journeys 13, 14, 20), with a
/// repeated completion completing it once. Workers cannot ask for or assign work, and another tenant's work does not exist.
/// </summary>
public sealed class HumanActionCapabilitiesTests : IAsyncLifetime
{
    private static readonly Actor agent = Actor.Agent("guestservice.request_work");
    private static readonly Actor router = Actor.Agent("router.test");
    private static readonly StaffMemberId sam = new(Guid.Parse("01890a5d-ac96-774b-bcce-00000000a001"));
    private static readonly StaffMemberId jo = new(Guid.Parse("01890a5d-ac96-774b-bcce-00000000a002"));
    private static readonly Actor samWorker = Actor.Staff(sam.ToString(), StaffRole.Worker);
    private static readonly Actor joWorker = Actor.Staff(jo.ToString(), StaffRole.Worker);
    private static readonly ActionLocation room412 = new("Room 412", Seed.Room412, Seed.Room412Space);

    private readonly StaffApi api = new(seedAuroraGrand: true);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Create_AsAnAgent_RequestsTheWorkForTheServiceRequest_AsTheCaller()
    {
        ServiceRequestId request = await ServiceRequestAsync();

        HumanActionEntry action = await CreateAsync(Towels(request), "work-1");

        Assert.Equal(
            (ActionType.DeliverItems, ActionStatus.Requested, "Deliver 2 × Bath towel", room412, ActionPriority.Normal, StaffApi.Now.AddMinutes(30)),
            (action.ActionType, action.Status, action.Instructions, action.Location, action.Priority, action.Deadline));
        Assert.Equal([new ActionItemEntry("Bath towel", 2, "bath_towel")], action.Items);
        Assert.Equal(["housekeeping"], action.SkillsRequired);
        Assert.Equal(((StaffMemberId?)null, agent.Id!, (ServiceRequestId?)request), (action.AssignedTo, action.RequestedBy, action.ServiceRequestId));
        Assert.Equal(("HumanAction", 1L), (action.Source.Type, action.Source.Version));
        Assert.Equal(["HumanActionRequested"], await EventsOfAsync(action));
    }

    [Fact]
    public async Task Create_AgainWithTheSameKey_ReturnsTheFirstWork_AndRequestsNothingMore()
    {
        ServiceRequestId request = await ServiceRequestAsync();
        HumanActionEntry first = await CreateAsync(Towels(request), "work-1");

        Result<CapabilityResponse> again = await RunAsync(agent, "HumanAction.Create", Towels(request), "work-1");

        Assert.True(again.Value.Replayed);
        Assert.Equal(first, Output(again).Action);
        Assert.Single(await api.EventsAsync(Seed.TenantId, Cancellation), message => message.EventType == "HumanActionRequested");
    }

    [Fact]
    public async Task Create_IsRefused_ToWorkers_ForAnUnknownServiceRequest_AndForADeliveryOfNothing()
    {
        CreateActionInput towels = Towels(null);

        Error worker = (await RunAsync(samWorker, "HumanAction.Create", towels, "work-1")).Error;
        Error unknown = (await RunAsync(agent, "HumanAction.Create", towels with { ServiceRequestId = new ServiceRequestId(Guid.CreateVersion7()) }, "work-2")).Error;
        Error nothing = (await RunAsync(agent, "HumanAction.Create", towels with { Items = [] }, "work-3")).Error;

        Assert.Equal((ErrorKind.Forbidden, "capability.forbidden"), (worker.Kind, worker.Code));
        Assert.Equal("human_action.service_request_not_found", Assert.Single(Assert.IsType<ValidationError>(unknown).Fields).Code);
        Assert.Equal("human_action.items_required", Assert.Single(Assert.IsType<ValidationError>(nothing).Fields).Code);
        Assert.DoesNotContain(await api.EventsAsync(Seed.TenantId, Cancellation), message => message.EventType == "HumanActionRequested");
    }

    [Fact]
    public async Task TheAssignee_AcceptsStartsAndCompletesTheWork_EachRaisingItsEvent_AndCompletingAgainChangesNothing()
    {
        HumanActionEntry action = await CreateAsync(Towels(null), "work-1");

        HumanActionEntry assigned = await SucceedAsync(router, "HumanAction.Assign", new AssignActionInput(action.Source.Id, sam));
        HumanActionEntry accepted = await SucceedAsync(samWorker, "HumanAction.UpdateStatus", new UpdateActionStatusInput(action.Source.Id, ActionStatus.Accepted));
        HumanActionEntry started = await SucceedAsync(samWorker, "HumanAction.UpdateStatus", new UpdateActionStatusInput(action.Source.Id, ActionStatus.InProgress));
        HumanActionEntry completed = await SucceedAsync(samWorker, "HumanAction.Complete", new CompleteActionInput(action.Source.Id, "Left at the door"));
        HumanActionEntry again = await SucceedAsync(samWorker, "HumanAction.Complete", new CompleteActionInput(action.Source.Id, "Done twice"));

        Assert.Equal(
            [(ActionStatus.Assigned, (StaffMemberId?)sam), (ActionStatus.Accepted, sam), (ActionStatus.InProgress, sam), (ActionStatus.Completed, sam), (ActionStatus.Completed, sam)],
            new[] { assigned, accepted, started, completed, again }.Select(entry => (entry.Status, entry.AssignedTo)));
        Assert.Equal(completed, again);
        Assert.Equal(
            ["HumanActionRequested", "HumanActionAssigned", "HumanActionAccepted", "HumanActionStarted", "HumanActionCompleted"],
            await EventsOfAsync(action));
    }

    [Fact]
    public async Task OnlyTheAssignee_MayAcceptOrComplete_NotAnotherWorkerOrAManager()
    {
        HumanActionEntry action = await CreateAsync(Towels(null), "work-1");
        await SucceedAsync(router, "HumanAction.Assign", new AssignActionInput(action.Source.Id, sam));

        Error other = (await RunAsync(joWorker, "HumanAction.UpdateStatus", new UpdateActionStatusInput(action.Source.Id, ActionStatus.Accepted))).Error;
        Error manager = (await RunAsync(Actor.Staff(Guid.CreateVersion7().ToString(), StaffRole.Manager), "HumanAction.Complete", new CompleteActionInput(action.Source.Id))).Error;
        Error nameless = (await RunAsync(Actor.Staff("maria", StaffRole.Worker), "HumanAction.Complete", new CompleteActionInput(action.Source.Id))).Error;

        Assert.Equal(
            [("human_action.not_assignee", ErrorKind.InvalidState), ("human_action.not_assignee", ErrorKind.InvalidState), ("human_action.not_staff_member", ErrorKind.Forbidden)],
            new[] { other, manager, nameless }.Select(error => (error.Code, error.Kind)));
        Assert.Equal(["HumanActionRequested", "HumanActionAssigned"], await EventsOfAsync(action));
    }

    [Fact]
    public async Task Reassigning_MovesTheWork_SoOnlyTheNewAssigneeMayAccept()
    {
        HumanActionEntry action = await CreateAsync(Towels(null), "work-1");
        await SucceedAsync(router, "HumanAction.Assign", new AssignActionInput(action.Source.Id, sam));

        HumanActionEntry reassigned = await SucceedAsync(Actor.Staff("maria", StaffRole.Manager), "HumanAction.Assign", new AssignActionInput(action.Source.Id, jo));
        Error formerAssignee = (await RunAsync(samWorker, "HumanAction.UpdateStatus", new UpdateActionStatusInput(action.Source.Id, ActionStatus.Accepted))).Error;
        Error worker = (await RunAsync(samWorker, "HumanAction.Assign", new AssignActionInput(action.Source.Id, sam))).Error;

        Assert.Equal((ActionStatus.Assigned, (StaffMemberId?)jo), (reassigned.Status, reassigned.AssignedTo));
        Assert.Equal(("human_action.not_assignee", "capability.forbidden"), (formerAssignee.Code, worker.Code));
        Assert.Equal(["HumanActionRequested", "HumanActionAssigned", "HumanActionAssigned"], await EventsOfAsync(action));
    }

    [Fact]
    public async Task TheAssignee_FailsTheWorkWithAReason_WhichIsRequired_AndCannotSetAnyOtherStatus()
    {
        HumanActionEntry action = await CreateAsync(Towels(null), "work-1");
        await SucceedAsync(router, "HumanAction.Assign", new AssignActionInput(action.Source.Id, sam));
        await SucceedAsync(samWorker, "HumanAction.UpdateStatus", new UpdateActionStatusInput(action.Source.Id, ActionStatus.Accepted));

        Error noReason = (await RunAsync(samWorker, "HumanAction.UpdateStatus", new UpdateActionStatusInput(action.Source.Id, ActionStatus.Failed))).Error;
        Error completed = (await RunAsync(samWorker, "HumanAction.UpdateStatus", new UpdateActionStatusInput(action.Source.Id, ActionStatus.Completed))).Error;
        HumanActionEntry failed = await SucceedAsync(samWorker, "HumanAction.UpdateStatus", new UpdateActionStatusInput(action.Source.Id, ActionStatus.Failed, "Could not access the room"));

        Assert.Equal(
            ["human_action.reason_required", "human_action.status_not_settable"],
            new[] { noReason, completed }.Select(error => Assert.Single(Assert.IsType<ValidationError>(error).Fields).Code));
        Assert.Equal((ActionStatus.Failed, "Could not access the room"), (failed.Status, failed.FailureReason));
        IReadOnlyList<EventMessage> events = await api.EventsAsync(Seed.TenantId, Cancellation);
        EventMessage failure = Assert.Single(events, message => message.EventType == "HumanActionFailed");
        using JsonDocument envelope = JsonDocument.Parse(failure.Envelope);
        Assert.Equal("Could not access the room", envelope.RootElement.GetProperty("payload").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task AnotherTenantsWork_DoesNotExist()
    {
        HumanActionEntry action = await CreateAsync(Towels(null), "work-1");

        Result<CapabilityResponse> elsewhere = await RunAsync(router, "HumanAction.Assign", new AssignActionInput(action.Source.Id, sam), tenant: new TenantId(Guid.CreateVersion7()));

        Assert.Equal(("human_action.not_found", ErrorKind.NotFound), (elsewhere.Error.Code, elsewhere.Error.Kind));
    }

    private static CreateActionInput Towels(ServiceRequestId? request) => new(
        Seed.PropertyId,
        ActionType.DeliverItems,
        "Deliver 2 × Bath towel",
        room412,
        "The guest asked for it in a message to the concierge.",
        SlaMinutes: 30,
        Items: [new ActionItemEntry("Bath towel", 2, "bath_towel")],
        SkillsRequired: ["housekeeping"],
        ServiceRequestId: request);

    private static HumanActionOutput Output(Result<CapabilityResponse> result) =>
        result.Value.Output.Deserialize<HumanActionOutput>(ContractJson.Options)!;

    private async Task<ServiceRequestId> ServiceRequestAsync()
    {
        Result<CapabilityResponse> created = await RunAsync(
            agent, "GuestService.CreateRequest", new CreateRequestInput(Seed.AlexStay, "housekeeping.amenities", "bath_towel", 2), "request-1");
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

        return new ServiceRequestId(created.Value.Output.Deserialize<CreateRequestOutput>(ContractJson.Options)!.Request.Source.Id);
    }

    private async Task<HumanActionEntry> CreateAsync(CreateActionInput input, string key) => await SucceedAsync(agent, "HumanAction.Create", input, key);

    private async Task<HumanActionEntry> SucceedAsync(Actor actor, string capability, object input, string? key = null)
    {
        Result<CapabilityResponse> result = await RunAsync(actor, capability, input, key);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);

        return Output(result).Action;
    }

    private async Task<Result<CapabilityResponse>> RunAsync(Actor actor, string capability, object input, string? key = null, TenantId? tenant = null)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant ?? Seed.TenantId);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(actor);

        return await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(
            new CapabilityRequest(capability, JsonSerializer.SerializeToElement(input, ContractJson.Options), IdempotencyKey: key), Cancellation);
    }

    private async Task<IReadOnlyList<string>> EventsOfAsync(HumanActionEntry action) =>
        [.. (await api.EventsAsync(Seed.TenantId, Cancellation)).Where(message => message.AggregateId == action.Source.Id).Select(message => message.EventType)];
}
