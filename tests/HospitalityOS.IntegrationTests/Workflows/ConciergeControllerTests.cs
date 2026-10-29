using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Eventing.Replay;
using HospitalityOS.IntegrationTests.Capabilities.Reading;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.Workflows.Concierge;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Workflows;

/// <summary>
/// The Concierge Controller in the real host, end to end over HTTP with events dispatched and the Fake model scripted like
/// a concierge (Journey 5): a guest's question gets a grounded answer in the conversation, raised as
/// <c>GuestQuestionAnswered</c> after the answer and in the question's flow, with a trace of the turn; a question no
/// source answers gets the honest answer (Journey 17); a redelivered message is answered once (P8); and when the model
/// cannot be reached the guest is told so and the trace records the failure.
/// </summary>
public sealed class ConciergeControllerTests : IAsyncLifetime
{
    private static readonly Guid invented = Guid.Parse("01890a5d-ac96-774b-bcce-0000000dead1");

    private readonly AuroraGrand aurora = new(new TenantId(Guid.CreateVersion7()));
    private readonly StaffApi api;
    private GuestStay alex = null!;

    public ConciergeControllerTests()
    {
        api = Host(aurora, builder => { });
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => alex = await PrepareAsync(api);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task PoolQuestion_IsAnsweredInTheConversation_ThenGuestQuestionAnswered_InTheQuestionsFlow_WithATrace()
    {
        using HttpClient guest = api.Guest(aurora.TenantId, alex);
        ConversationResponse conversation = await StartAsync(guest);

        GuestMessageResponse question = await PostAsync(guest, conversation, "What time does the pool close tonight?");
        IReadOnlyList<GuestMessageResponse> messages = await MessagesUntilAsync(api, guest, conversation, 2);

        GuestMessageResponse answer = messages[1];
        Assert.Equal(question, messages[0]);
        Assert.Equal((MessageSender.Concierge, "The pool closes at 22:00 tonight."), (answer.Sender, answer.Text));

        IReadOnlyList<EventMessage> events = await api.EventsAsync(aurora.TenantId, Cancellation);
        EventMessage received = Assert.Single(events, message => message.EventType == "GuestMessageReceived");
        EventMessage answered = Assert.Single(events, message => message.EventType == "GuestQuestionAnswered");
        EventMessage sent = Assert.Single(events, message => message.EventType == "GuestMessageSent");
        Assert.Equal((question.Id.Value, answer.Id.Value, answer.Id.Value), (received.AggregateId, sent.AggregateId, answered.AggregateId));
        Assert.True(events.ToList().IndexOf(received) < events.ToList().IndexOf(sent) && events.ToList().IndexOf(sent) < events.ToList().IndexOf(answered));
        Assert.Equal((received.CorrelationId, received.CorrelationId), (sent.CorrelationId, answered.CorrelationId));
        Assert.Equal((question.Id.Value, answer.Id.Value, "Answered"), Payload(answered));

        AITraceResponse trace = await TraceAsync(api, received.CorrelationId);
        Assert.Equal(
            (AITraceOutcome.Answered, Concierge.AnswerTask, Concierge.QuestionIntent, "GuestMessage", question.Id.Value, received.EventId),
            (trace.Outcome, trace.Agent, trace.Intent, trace.TriggerType, trace.TriggerId, trace.CausationId));
        Assert.Equal(question.Text, trace.Body.TriggerText);
        Assert.Equal(new TraceResponse(answer.Text, [new SourceReference("OperatingHours", aurora.PoolHours.Id.Value, 1)]), trace.Body.Response);
        Assert.Contains(trace.FlowEvents, flow => flow.EventType == "GuestQuestionAnswered");
    }

    [Fact]
    public async Task AQuestionNoSourceAnswers_GetsTheHonestAnswer_NeverTheInventedOne()
    {
        using HttpClient guest = api.Guest(aurora.TenantId, alex);
        ConversationResponse conversation = await StartAsync(guest);

        await PostAsync(guest, conversation, "Do you offer free helicopter transfers?");
        IReadOnlyList<GuestMessageResponse> messages = await MessagesUntilAsync(api, guest, conversation, 2);

        Assert.Equal((MessageSender.Concierge, Concierge.NoSourceAnswer), (messages[1].Sender, messages[1].Text));
        EventMessage answered = Assert.Single(await api.EventsAsync(aurora.TenantId, Cancellation), message => message.EventType == "GuestQuestionAnswered");
        Assert.Equal("NoAuthoritativeSource", Payload(answered).Outcome);
        Assert.Equal(AITraceOutcome.NoAuthoritativeSource, (await TraceAsync(api, answered.CorrelationId)).Outcome);
    }

    [Fact]
    public async Task ARedeliveredMessage_IsAnsweredOnce()
    {
        using HttpClient guest = api.Guest(aurora.TenantId, alex);
        ConversationResponse conversation = await StartAsync(guest);
        await PostAsync(guest, conversation, "What time does the pool close tonight?");
        await MessagesUntilAsync(api, guest, conversation, 2);

        await using (AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IEventReplayer>().ReplayAsync(new EventReplayRequest(TenantId: aurora.TenantId), Cancellation) > 0);
        }

        // A later question is answered after the replayed deliveries, which by then were skipped as already processed.
        await PostAsync(guest, conversation, "Do you offer free helicopter transfers?");
        IReadOnlyList<GuestMessageResponse> messages = await MessagesUntilAsync(api, guest, conversation, 4);

        Assert.Equal(
            [MessageSender.Guest, MessageSender.Concierge, MessageSender.Guest, MessageSender.Concierge],
            messages.Select(message => message.Sender));
        Assert.Equal(2, (await api.EventsAsync(aurora.TenantId, Cancellation)).Count(message => message.EventType == "GuestQuestionAnswered"));
    }

    [Fact]
    public async Task WhenTheModelCannotBeReached_TheGuestIsToldSo_AndTheTraceRecordsTheFailure()
    {
        await using StaffApi unreachable = Host(aurora, builder =>
        {
            builder.UseSetting("Inference:Providers:claude:Adapter", "Anthropic");
            builder.UseSetting("Inference:Providers:claude:Settings:BaseUrl", "https://127.0.0.1:9");
            builder.UseSetting("Inference:Providers:claude:Settings:ApiKeyVariable", "HOSPITALITYOS_TESTS_NO_SUCH_KEY");
            builder.UseSetting("Inference:Providers:claude:Settings:MaxRetries", "0");
            builder.UseSetting($"Inference:Tasks:{Concierge.AnswerTask}:Provider", "claude");
            builder.UseSetting($"Inference:Tasks:{Concierge.AnswerTask}:Model", "claude-sonnet-5-5");
        });
        GuestStay stay = await PrepareAsync(unreachable);
        using HttpClient guest = unreachable.Guest(aurora.TenantId, stay);
        ConversationResponse conversation = await StartAsync(guest);

        await PostAsync(guest, conversation, "What time does the pool close tonight?");
        IReadOnlyList<GuestMessageResponse> messages = await MessagesUntilAsync(unreachable, guest, conversation, 2);

        Assert.Equal((MessageSender.Concierge, Concierge.UnavailableAnswer), (messages[1].Sender, messages[1].Text));
        IReadOnlyList<EventMessage> events = await unreachable.EventsAsync(aurora.TenantId, Cancellation);
        Assert.DoesNotContain(events, message => message.EventType == "GuestQuestionAnswered");
        AITraceResponse trace = await TraceAsync(unreachable, Assert.Single(events, message => message.EventType == "GuestMessageReceived").CorrelationId);
        Assert.Equal((AITraceOutcome.Failed, Concierge.UnavailableAnswer), (trace.Outcome, trace.Body.Response?.Text));
        Assert.NotEmpty(trace.Body.ModelCalls);
    }

    private static StaffApi Host(AuroraGrand aurora, Action<IWebHostBuilder> configure)
    {
        FakeScriptOptions concierge = new();
        concierge.Chat.Add(new FakeChatRuleOptions(
            AfterTool: "Property_GetOperatingHours",
            Reply: $$"""{"status":"Answered","answer":"The pool closes at 22:00 tonight.","citations":[{"type":"OperatingHours","id":"{{aurora.PoolHours.Id.Value}}","version":1}]}"""));
        concierge.Chat.Add(new FakeChatRuleOptions(
            WhenUserSays: "pool",
            CallTool: "Property_GetOperatingHours",
            Arguments: $$"""{"propertyId":"{{aurora.PropertyId.Value}}","subject":"Amenity","subjectId":"{{aurora.Pool.Id.Value}}"}"""));
        concierge.Chat.Add(new FakeChatRuleOptions(
            WhenUserSays: "helicopter",
            Reply: $$"""{"status":"Answered","answer":"Yes, helicopter transfers are free.","citations":[{"type":"Policy","id":"{{invented}}","version":1}]}"""));

        return GuestChat.Host(concierge, configure);
    }

    private async Task<GuestStay> PrepareAsync(StaffApi host)
    {
        await aurora.SaveAsync(host.Factory.Services, Cancellation);
        await aurora.IndexContentAsync(host.Factory.Services, Cancellation);

        return await GuestStay.SaveAsync(host.Factory.Services, aurora.TenantId, aurora.PropertyId, host.Clock.GetUtcNow(), Cancellation);
    }

    private static Task<ConversationResponse> StartAsync(HttpClient guest) => GuestChat.StartAsync(guest, Cancellation);

    private static Task<GuestMessageResponse> PostAsync(HttpClient guest, ConversationResponse conversation, string text) =>
        GuestChat.PostAsync(guest, conversation, text, Cancellation);

    private static Task<IReadOnlyList<GuestMessageResponse>> MessagesUntilAsync(StaffApi host, HttpClient guest, ConversationResponse conversation, int count) =>
        GuestChat.MessagesUntilAsync(host, guest, conversation, count, Cancellation);

    private Task<AITraceResponse> TraceAsync(StaffApi host, Guid correlationId) => GuestChat.TraceAsync(host, aurora.TenantId, correlationId, Cancellation);

    private static (Guid Question, Guid Answer, string? Outcome) Payload(EventMessage answered) => GuestChat.Payload(answered);
}
