using System.Text.Json;
using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.GuestService;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Eventing.Replay;
using HospitalityOS.Integrations.Mock.Messaging;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.Workflows.Concierge;
using HospitalityOS.Workflows.GuestService;
using HospitalityOS.Workflows.Routing;
using HospitalityOS.Workflows.Triage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static HospitalityOS.IntegrationTests.Workflows.GuestChat;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Workflows;

/// <summary>
/// Triage and the Guest Service Controller end to end over HTTP against the seeded Aurora Grand, where Alex is in house in
/// room 412, with the Fake model scripted to classify and read requests as a model would: two towels, a pillow, and a
/// broken AC each become one canonical <c>ServiceRequest</c> for room 412 in the message's flow and are acknowledged
/// without a promised time (Journeys 9 to 11); a redelivered message creates nothing more (P8); a request the model cannot
/// read is answered with a request for detail, and one the service refuses is passed to the front desk; and "where are my
/// towels?" goes to the concierge with Alex's own requests in its context (Journey 12).
/// </summary>
public sealed class GuestServiceControllerTests : IAsyncLifetime
{
    private static readonly GuestStay alex = new(Seed.Alex, Seed.AlexStay, Seed.PropertyId);

    private readonly StaffApi api = new(seedAuroraGrand: true, dispatchEvents: true, configure: Scripted);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// Waits until the seeded roster's joining has been routed, so it cannot race the test's own work.
    /// </summary>
    public async ValueTask InitializeAsync() =>
        await api.HandledAsync(Seed.TenantId, "StaffMemberCreated", DutyRerouting.JoinedName, Cancellation);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task TwoTowels_BecomeOneRequestForRoom412_AcknowledgedInTheMessagesFlow()
    {
        (GuestMessageResponse question, GuestMessageResponse answer) = await AskAsGuestAsync("Can you send two towels to my room?");

        Assert.Equal((MessageSender.Concierge, "Of course. I've asked our team to bring 2 bath towels to Room 412."), (answer.Sender, answer.Text));
        ServiceRequest request = await RequestForAsync(question);
        Assert.Equal(
            (ServiceCategory.Housekeeping, "bath_towel", 2, "Room 412", Seed.Room412, ServiceRequestStatus.Open),
            (request.ServiceCategory, request.Details.ItemCode, request.Details.Quantity, request.Details.Location.Label, request.Details.Location.AccommodationUnitId, request.Status));
        Assert.Equal((Seed.Alex, Seed.AlexStay, question.ConversationId, question.Id), (request.Origin.GuestId, request.Origin.StayContextId, request.Origin.ConversationId, request.Origin.SourceMessageId));

        HumanAction work = await WorkForAsync(request);
        Assert.Equal(
            (HumanActionType.DeliverItems, "Deliver 2 × Bath towel", request.Details.Location, HumanActionPriority.Normal, request.DueAt),
            (work.Details.ActionType, work.Details.Instructions, work.Details.Location, work.Details.Priority, work.Deadline));
        Assert.Equal([new ActionItem("Bath towel", 2, "bath_towel")], work.Details.Items!);
        Assert.Equal(["housekeeping"], work.Details.SkillsRequired!);
        Assert.Equal(WorkRequester.Name, work.Details.RequestedByAgent);

        IReadOnlyList<EventMessage> flow = await FlowAsync(question);
        EventMessage requested = Assert.Single(flow, message => message.EventType == "ServiceRequestCreated" && message.AggregateId == request.Id.Value);
        Assert.Equal(requested.EventId, work.Details.SourceEventId);
        Assert.Contains(flow, message => message.EventType == "HumanActionRequested" && message.AggregateId == work.Id.Value);

        // Both housekeepers are free, so the router takes them by name (spec §9).
        HumanAction routed = await WorkForAsync(request, assigned: true);
        Assert.Equal((HumanActionStatus.Assigned, (StaffMemberId?)Seed.JoOkafor), (routed.Status, routed.AssignedTo));
        Assert.Contains(await FlowAsync(question), message => message.EventType == "HumanActionAssigned" && message.AggregateId == work.Id.Value);
        Assert.Contains(flow, message => message.EventType == "GuestMessageSent" && message.AggregateId == answer.Id.Value);
        Assert.DoesNotContain(flow, message => message.EventType == "GuestQuestionAnswered");

        AITraceResponse triage = await TraceAsync(api, Seed.TenantId, flow[0].CorrelationId, Cancellation, GuestTriage.Task);
        Assert.Equal((AITraceOutcome.Routed, GuestTriage.ServiceRequestLabel), (triage.Outcome, triage.Intent));
        Assert.Equal(ModelCallKind.Classification, Assert.Single(triage.Body.ModelCalls).Kind);

        AITraceResponse trace = await TraceAsync(api, Seed.TenantId, flow[0].CorrelationId, Cancellation, GuestServiceController.Task);
        Assert.Equal((AITraceOutcome.Answered, GuestServiceController.Intent), (trace.Outcome, trace.Intent));
        CapabilityCall created = Assert.Single(trace.Body.CapabilityCalls);
        Assert.Equal((CapabilityCallPurpose.Action, "GuestService.CreateRequest@1", (string?)null), (created.Purpose, created.Capability, created.ErrorCode));
        Assert.Equal([new SourceReference("ServiceRequest", request.Id.Value, 1)], trace.Body.Response!.Citations);
    }

    [Fact]
    public async Task TheTowelJourney_FromTheGuestsMessageToTheGuestBeingTold_SharesOneCorrelationId_AndTellsTheGuestOnce()
    {
        (GuestMessageResponse question, _) = await AskAsGuestAsync("Can you send two towels to my room?");
        ServiceRequest request = await RequestForAsync(question);
        HumanAction work = await WorkForAsync(request, assigned: true);
        Actor assignee = Actor.Staff(work.AssignedTo!.Value.ToString(), StaffRole.Worker);

        await ActOnAsync(assignee, "HumanAction.UpdateStatus", new UpdateActionStatusInput(work.Id.Value, ActionStatus.Accepted), work);
        await ActOnAsync(assignee, "HumanAction.UpdateStatus", new UpdateActionStatusInput(work.Id.Value, ActionStatus.InProgress), work);
        await RequestUntilAsync(question, ServiceRequestStatus.InProgress);
        await ActOnAsync(assignee, "HumanAction.Complete", new CompleteActionInput(work.Id.Value, "Left at the door"), work);

        string[] journey =
        [
            "GuestMessageReceived", "GuestMessageTriaged", "ServiceRequestCreated", "HumanActionRequested", "HumanActionAssigned",
            "HumanActionAccepted", "HumanActionStarted", "ServiceRequestUpdated", "HumanActionCompleted", "ServiceRequestCompleted",
            "GuestNotificationRequested", "GuestNotificationSent",
        ];
        IReadOnlyList<EventMessage> flow = await FlowUntilAsync(question, "GuestNotificationSent");
        Assert.All(journey, type => Assert.Single(flow, message => message.EventType == type));

        // The acknowledgement and the news that it is done.
        Assert.Equal(2, flow.Count(message => message.EventType == "GuestMessageSent"));

        const string done = "Good news: your 2 bath towels have been delivered to Room 412.";
        using HttpClient guest = api.Guest(Seed.TenantId, alex);
        ConversationResponse conversation = new(question.ConversationId, Seed.PropertyId, Seed.AlexStay, ConversationStatus.Open, default);
        IReadOnlyList<GuestMessageResponse> messages = await MessagesUntilAsync(api, guest, conversation, 3, Cancellation);
        Assert.Equal((MessageSender.Concierge, done), (messages[^1].Sender, messages[^1].Text));

        // Delivered again, completion tells the guest nothing more (Journey 19).
        await using (AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IEventReplayer>().ReplayAsync(new EventReplayRequest(TenantId: Seed.TenantId), Cancellation) > 0);
        }

        api.Clock.Advance(TimeSpan.FromSeconds(5));
        await AskAsGuestAsync("We need another pillow.");
        Assert.Single(await api.EventsAsync(Seed.TenantId, Cancellation), message => message.EventType == "GuestNotificationSent");
        Assert.Equal([done], api.Factory.Services.GetRequiredService<MockMessagingGateway>().DeliveredFor(Seed.PropertyId).Select(delivered => delivered.Message.Body));
        Assert.Equal(3, (await MessagesUntilAsync(api, guest, conversation, 3, Cancellation)).Count);
    }

    [Fact]
    public async Task APillow_IsOneUnlessTheGuestSaysOtherwise_ThroughTheSameDeliveryWorkflow()
    {
        (GuestMessageResponse question, GuestMessageResponse answer) = await AskAsGuestAsync("We need another pillow.");

        Assert.Equal("Of course. I've asked our team to bring a pillow to Room 412.", answer.Text);
        ServiceRequest request = await RequestForAsync(question);
        Assert.Equal(("pillow", 1, ServiceCategory.Housekeeping), (request.Details.ItemCode, request.Details.Quantity, request.ServiceCategory));
        Assert.Equal([new ActionItem("Pillow", 1, "pillow")], (await WorkForAsync(request)).Details.Items!);
    }

    [Fact]
    public async Task ABrokenAirConditioner_BecomesAMaintenanceRequestInTheGuestsWords_AndIsAcknowledged()
    {
        (GuestMessageResponse question, GuestMessageResponse answer) = await AskAsGuestAsync("The AC isn't working.");

        Assert.Equal("Thanks for letting us know. I've asked our team to take care of it in Room 412.", answer.Text);
        ServiceRequest request = await RequestForAsync(question);
        Assert.Equal(
            (ServiceCategory.Maintenance, (string?)null, "The air conditioning is not working"),
            (request.ServiceCategory, request.Details.ItemCode, request.Details.Description));

        HumanAction work = await WorkForAsync(request);
        Assert.Equal(
            (HumanActionType.CheckRoom, "Repairs: The air conditioning is not working", "Room 412", 0),
            (work.Details.ActionType, work.Details.Instructions, work.Details.Location.Label, work.Details.Items!.Count));
        Assert.Equal(["maintenance"], work.Details.SkillsRequired!);
    }

    [Fact]
    public async Task ARedeliveredRequest_CreatesNothingMore()
    {
        (GuestMessageResponse question, _) = await AskAsGuestAsync("Can you send two towels to my room?");
        await WorkForAsync(await RequestForAsync(question));

        await using (AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IEventReplayer>().ReplayAsync(new EventReplayRequest(TenantId: Seed.TenantId), Cancellation) > 0);
        }

        // A later message is handled after the replayed deliveries, which by then were skipped as already processed.
        api.Clock.Advance(TimeSpan.FromSeconds(5));
        (GuestMessageResponse pillow, _) = await AskAsGuestAsync("We need another pillow.");
        await WorkForAsync(await RequestForAsync(pillow));

        IReadOnlyList<EventMessage> events = await api.EventsAsync(Seed.TenantId, Cancellation);
        Assert.Equal(2, events.Count(message => message.EventType == "ServiceRequestCreated"));
        Assert.Equal(2, events.Count(message => message.EventType == "HumanActionRequested"));
        Assert.Single(events, message => message.EventType == "GuestMessageTriaged" && message.AggregateId == question.Id.Value);
        await WorkForAsync(await RequestForAsync(question));
    }

    [Fact]
    public async Task ARequestTheModelCannotRead_GetsARequestForDetail_AndCreatesNothing()
    {
        (GuestMessageResponse question, GuestMessageResponse answer) = await AskAsGuestAsync("Can you send a unicorn up?");

        Assert.Equal(GuestServiceController.ClarifyAnswer, answer.Text);
        Assert.Null(await FindRequestAsync(question));
        AITraceResponse trace = await TraceAsync(api, Seed.TenantId, (await FlowAsync(question))[0].CorrelationId, Cancellation, GuestServiceController.Task);
        Assert.Equal(AITraceOutcome.Uncertain, trace.Outcome);
    }

    [Fact]
    public async Task ARequestTheServiceRefuses_IsPassedToTheFrontDesk()
    {
        // Housekeeping delivers at most four bath towels per request.
        (GuestMessageResponse question, GuestMessageResponse answer) = await AskAsGuestAsync("Please send nine towels.");

        Assert.Equal(GuestServiceController.RefusedAnswer, answer.Text);
        Assert.Null(await FindRequestAsync(question));
        AITraceResponse trace = await TraceAsync(api, Seed.TenantId, (await FlowAsync(question))[0].CorrelationId, Cancellation, GuestServiceController.Task);
        Assert.Equal(AITraceOutcome.Failed, trace.Outcome);
        Assert.NotNull(Assert.Single(trace.Body.CapabilityCalls).ErrorCode);
    }

    [Fact]
    public async Task WhereAreMyTowels_GoesToTheConcierge_WithTheGuestsOwnRequestsInItsContext()
    {
        (GuestMessageResponse towels, _) = await AskAsGuestAsync("Can you send two towels to my room?");
        ServiceRequest request = await RequestForAsync(towels);

        (GuestMessageResponse status, _) = await AskAsGuestAsync("Where are my towels?");

        AITraceResponse trace = await TraceAsync(api, Seed.TenantId, (await FlowAsync(status))[0].CorrelationId, Cancellation);
        Assert.Equal(Concierge.StatusIntent, trace.Intent);
        CapabilityCall read = Assert.Single(trace.Body.CapabilityCalls, call => call.Capability == "GuestService.GetRequestStatus@1");
        Assert.Equal(CapabilityCallPurpose.Context, read.Purpose);
        Assert.Equal(Seed.AlexStay.Value, read.Input!.Value.GetProperty("stayContextId").GetGuid());
        ServiceRequestEntry pending = Assert.Single(read.Output!.Value.Deserialize<GetRequestStatusOutput>(ContractJson.Options)!.Requests);
        Assert.Equal((new SourceReference("ServiceRequest", request.Id.Value, 1), RequestStatus.Open, "bath_towel"), (pending.Source, pending.Status, pending.ItemCode));
        Assert.Contains(new SourceReference("ServiceRequest", request.Id.Value, 1), trace.Body.Retrieved);
    }

    private static void Scripted(IWebHostBuilder builder)
    {
        FakeScriptOptions script = new();
        script.Classification.Add(new FakeClassificationRuleOptions("where are my", GuestTriage.RequestStatusLabel));
        script.Classification.Add(new FakeClassificationRuleOptions("towel", GuestTriage.ServiceRequestLabel));
        script.Classification.Add(new FakeClassificationRuleOptions("pillow", GuestTriage.ServiceRequestLabel));
        script.Classification.Add(new FakeClassificationRuleOptions("AC", GuestTriage.ServiceRequestLabel));
        script.Classification.Add(new FakeClassificationRuleOptions("unicorn", GuestTriage.ServiceRequestLabel));
        script.Extraction.Add(Read("nine towels", "housekeeping.amenities", "bath_towel", 9));
        script.Extraction.Add(Read("two towels", "housekeeping.amenities", "bath_towel", 2));
        script.Extraction.Add(Read("pillow", "housekeeping.amenities", "pillow", null));
        script.Extraction.Add(new FakeExtractionRuleOptions("AC", """{"understood":true,"serviceCode":"maintenance.repair","itemCode":null,"quantity":null,"description":"The air conditioning is not working"}"""));
        script.Extraction.Add(new FakeExtractionRuleOptions("unicorn", """{"understood":false,"serviceCode":null,"itemCode":null,"quantity":null,"description":null}"""));
        script.Chat.Add(new FakeChatRuleOptions(Reply: """{"status":"Unknown","answer":"","citations":[]}"""));

        builder.UseSetting("Inference:Providers:scripted:Adapter", "Fake");
        builder.UseSetting("Inference:Providers:scripted:Settings:Script", "guests");

        foreach (string task in new[] { GuestTriage.Task, GuestServiceController.Task, Concierge.AnswerTask })
        {
            builder.UseSetting($"Inference:Tasks:{task}:Provider", "scripted");
            builder.UseSetting($"Inference:Tasks:{task}:Model", "fake");
        }

        builder.ConfigureTestServices(services => services.Configure<FakeInferenceOptions>(options => options.Scripts["guests"] = script));

        static FakeExtractionRuleOptions Read(string says, string service, string item, int? quantity) =>
            new(says, JsonSerializer.Serialize(new { understood = true, serviceCode = service, itemCode = item, quantity, description = (string?)null }));
    }

    private async Task<(GuestMessageResponse Question, GuestMessageResponse Answer)> AskAsGuestAsync(string text)
    {
        using HttpClient guest = api.Guest(Seed.TenantId, alex);

        return await AskAsync(api, guest, text, Cancellation);
    }

    private async Task<IReadOnlyList<EventMessage>> FlowAsync(GuestMessageResponse question)
    {
        IReadOnlyList<EventMessage> events = await api.EventsAsync(Seed.TenantId, Cancellation);
        Guid correlation = events.Single(message => message.EventType == "GuestMessageReceived" && message.AggregateId == question.Id.Value).CorrelationId;

        return [.. events.Where(message => message.CorrelationId == correlation)];
    }

    /// <summary>
    /// The message's flow once it holds an event of <paramref name="type"/>: events reach the store as they are dispatched.
    /// </summary>
    private async Task<IReadOnlyList<EventMessage>> FlowUntilAsync(GuestMessageResponse question, string type)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            IReadOnlyList<EventMessage> flow = await FlowAsync(question);

            if (flow.Any(message => message.EventType == type))
            {
                return flow;
            }

            api.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    /// <summary>
    /// Acts on work as the staff surface does, continuing the work's flow.
    /// </summary>
    private async Task ActOnAsync(Actor actor, string capability, object input, HumanAction work)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(actor);
        Result<CapabilityResponse> result = await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(
            new CapabilityRequest(capability, JsonSerializer.SerializeToElement(input, ContractJson.Options), ContinuesFlow: work.Details.CorrelationId),
            Cancellation);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);
    }

    /// <summary>
    /// The message's request once it reaches <paramref name="status"/>, as continuation moves it on.
    /// </summary>
    private async Task RequestUntilAsync(GuestMessageResponse question, ServiceRequestStatus status)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while ((await RequestForAsync(question)).Status != status)
        {
            api.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    private async Task<ServiceRequest> RequestForAsync(GuestMessageResponse question) =>
        await FindRequestAsync(question) ?? throw new Xunit.Sdk.XunitException($"No service request was created for '{question.Text}'.");

    /// <summary>
    /// The one human action asked for a request, waiting for it (and, when <paramref name="assigned"/>, for the router to
    /// assign it): it is requested after the request is acknowledged.
    /// </summary>
    private async Task<HumanAction> WorkForAsync(ServiceRequest request, bool assigned = false)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            await using (AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope())
            {
                scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);
                IReadOnlyList<HumanAction> work = await scope.ServiceProvider.GetRequiredService<IHumanActionRepository>().ListForServiceRequestAsync(request.Id, timeout.Token);

                if (work.Count > 0 && (!assigned || work[0].AssignedTo is not null))
                {
                    return Assert.Single(work);
                }
            }

            api.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    private async Task<ServiceRequest?> FindRequestAsync(GuestMessageResponse question)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);

        return await scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>().FindBySourceMessageAsync(question.Id, Cancellation);
    }
}
