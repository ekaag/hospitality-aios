using System.Net;
using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Ai.Grounding;
using HospitalityOS.Ai.Tracing;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.IntegrationTests.Capabilities.Reading;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AITraceOutcome = HospitalityOS.Contracts.Api.Ai.AITraceOutcome;

namespace HospitalityOS.IntegrationTests.Ai;

/// <summary>
/// The concierge's grounded answers in the real host, recorded as AI traces and read through <c>/api/v1/ai/traces</c>
/// (Journey 15), with the Fake model scripted like a concierge: the pool answer's trace names the OperatingHours record and
/// version it used, also after the hours change (Journey 16); an unknown question's trace shows that no authoritative
/// source was found (Journey 17); and what a reader sees depends on their role.
/// </summary>
public sealed class AITraceApiTests : IAsyncLifetime
{
    private const string Task = "concierge.answer";
    private const string Route = "/api/v1/ai/traces";
    private const string NoSource = "I don't have confirmed information about that.";

    private static readonly Guid invented = Guid.Parse("01890a5d-ac96-774b-bcce-0000000dead1");

    private readonly AuroraGrand aurora = new(new TenantId(Guid.CreateVersion7()));
    private readonly StaffApi api;

    public AITraceApiTests()
    {
        api = Host(aurora, builder => { });
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await aurora.SaveAsync(api.Factory.Services, Cancellation);
        await aurora.IndexContentAsync(api.Factory.Services, Cancellation);
    }

    public async ValueTask DisposeAsync() => await api.DisposeAsync();

    [Fact]
    public async Task PoolQuestion_TheTraceNamesTheOperatingHoursRecordAndVersion_AndEverythingThatLedThere()
    {
        Guid id = await AskAsync("What time does the pool close tonight?");

        AITraceResponse trace = await GetAsync(id, "Admin");

        SourceReference poolHours = new("OperatingHours", aurora.PoolHours.Id.Value, 1);
        Assert.Equal((AITraceOutcome.Answered, Task, "question", "GuestMessage"), (trace.Outcome, trace.Agent, trace.Intent, trace.TriggerType));
        Assert.Equal(aurora.PropertyId, trace.PropertyId);
        Assert.Equal("What time does the pool close tonight?", trace.Body.TriggerText);
        Assert.Equal(new TraceResponse("The pool closes at 22:00 tonight.", [poolHours]), trace.Body.Response);
        Assert.Contains(poolHours, trace.Body.Retrieved);
        Assert.Contains(new SourceReference("Property", aurora.PropertyId.Value, 1), trace.Body.Retrieved);
        Assert.Equal(
            [
                (CapabilityCallPurpose.Context, "Property.GetProperty@1"),
                (CapabilityCallPurpose.Context, "Property.SearchContent@1"),
                (CapabilityCallPurpose.Tool, "Property.GetOperatingHours@1"),
            ],
            trace.Body.CapabilityCalls.Select(call => (call.Purpose, call.Capability)));
        Assert.All(trace.Body.CapabilityCalls, call =>
        {
            Assert.Equal((AuthorizationDecision.Allowed, (string?)null), (call.Authorization, call.ErrorCode));
            Assert.NotNull(call.InvocationId);
        });
        Assert.All(trace.Body.ModelCalls.SelectMany(call => call.Attempts), attempt => Assert.Equal(
            ("scripted", "fake-concierge", "concierge.answer", 4),
            (attempt.Provider, attempt.Model, attempt.Prompt?.Name, attempt.Prompt?.Number ?? 0)));
        Assert.Contains("Answer only from authoritative sources", trace.Body.ModelCalls[0].SystemPrompt, StringComparison.Ordinal);
        Assert.Equal([new ReplyVerdict(true, null)], trace.Body.Grounding!.Replies);
        Assert.Contains(trace.FlowEvents, flowEvent => flowEvent.EventType == "AITraceCreated" && flowEvent.AggregateId == id);
        Assert.Contains(trace.FlowEvents, flowEvent => flowEvent.EventType == "CapabilityInvocationSucceeded");
        Assert.False(trace.Redacted);
    }

    [Fact]
    public async Task UnknownQuestion_TheTraceShowsThatNoAuthoritativeSourceWasFound()
    {
        Guid id = await AskAsync("Do you offer free helicopter transfers?");

        AITraceResponse trace = await GetAsync(id, "Manager");

        Assert.Equal(AITraceOutcome.NoAuthoritativeSource, trace.Outcome);
        Assert.Equal(new TraceResponse(NoSource, []), trace.Body.Response);
        Assert.Equal(2, trace.Body.Grounding!.Replies.Count);
        Assert.All(trace.Body.Grounding.Replies, verdict => Assert.False(verdict.Accepted));
        Assert.Contains($"{invented}", trace.Body.Grounding.Replies[0].Rejection, StringComparison.Ordinal);
        Assert.DoesNotContain(trace.Body.Retrieved, source => source.Id == invented);

        AITracePage unanswered = await ListAsync("Manager", "?outcome=NoAuthoritativeSource");
        Assert.Equal([id], unanswered.Traces.Select(summary => summary.Id));
    }

    [Fact]
    public async Task AfterTheHoursChange_NewAnswersUseTheNewVersion_AndTheOldTraceStillNamesTheVersionItUsed()
    {
        Guid before = await AskAsync("What time does the pool close tonight?");
        await ChangePoolHoursAsync();

        Guid after = await AskAsync("What time does the pool close tomorrow?");

        Assert.Equal([new SourceReference("OperatingHours", aurora.PoolHours.Id.Value, 1)], (await GetAsync(before, "Manager")).Body.Response.Citations);
        AITraceResponse changed = await GetAsync(after, "Manager");
        Assert.Equal([new SourceReference("OperatingHours", aurora.PoolHours.Id.Value, 2)], changed.Body.Response.Citations);
        Assert.Contains("\"version\":2", changed.Body.CapabilityCalls[2].Output?.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Manager", "When does the pool close? Write to me at [email].", true)]
    [InlineData("Admin", "When does the pool close? Write to me at alex@example.com.", false)]
    public async Task ATracesGuestText_IsRedactedForRolesThatMayNotSeeItWhole(string role, string shown, bool redacted)
    {
        Guid id = await AskAsync("When does the pool close? Write to me at alex@example.com.");

        AITraceResponse trace = await GetAsync(id, role);

        Assert.Equal((shown, redacted), (trace.Body.TriggerText, trace.Redacted));
        Assert.Equal(shown, trace.Body.ModelCalls[0].Input[0].Parts[0].Text);
    }

    [Fact]
    public async Task Traces_AreForManagersAndAdministratorsOfTheTenantOnly()
    {
        Guid id = await AskAsync("What time does the pool close tonight?");
        TenantId otherTenant = new(Guid.CreateVersion7());

        using HttpClient worker = api.Staff(aurora.TenantId, "Worker");
        using HttpClient anonymous = api.Client();
        using HttpClient otherManager = api.Staff(otherTenant, "Manager");

        Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync($"{Route}/{id}", Cancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route, Cancellation)).StatusCode);
        Assert.Equal((HttpStatusCode.NotFound, AITraceErrors.NotFoundCode), await StaffApi.ProblemAsync(await otherManager.GetAsync($"{Route}/{id}", Cancellation), Cancellation));
        Assert.Empty((await StaffApi.OkAsync<AITracePage>(await otherManager.GetAsync(Route, Cancellation), Cancellation)).Traces);
    }

    [Fact]
    public async Task List_IsNewestFirst_PagesWithItsCursor_AndFiltersByProperty()
    {
        Guid first = await AskAsync("What time does the pool close tonight?");
        api.Clock.Advance(TimeSpan.FromMinutes(1));
        Guid second = await AskAsync("Do you offer free helicopter transfers?");
        api.Clock.Advance(TimeSpan.FromMinutes(1));
        Guid third = await AskAsync("What time does the pool close tomorrow?");

        AITracePage page = await ListAsync("Manager", "?limit=2");
        AITracePage rest = await ListAsync("Manager", $"?limit=2&after={Uri.EscapeDataString(page.Next!)}");

        Assert.Equal([third, second], page.Traces.Select(summary => summary.Id));
        Assert.Equal([first], rest.Traces.Select(summary => summary.Id));
        Assert.Null(rest.Next);
        Assert.Equal(3, (await ListAsync("Admin", $"?propertyId={aurora.PropertyId.Value}")).Traces.Count);
        Assert.Empty((await ListAsync("Admin", $"?propertyId={Guid.CreateVersion7()}")).Traces);
    }

    [Theory]
    [InlineData("?after=not-a-cursor")]
    [InlineData("?limit=0")]
    [InlineData("?outcome=Guessed")]
    [InlineData("?propertyId=nope")]
    public async Task List_WithAnInvalidQuery_IsABadRequest(string query)
    {
        using HttpClient manager = api.Staff(aurora.TenantId, "Manager");

        using HttpResponseMessage response = await manager.GetAsync($"{Route}{query}", Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Startup_WithAnUnknownUnredactedRole_Fails()
    {
        await using StaffApi misconfigured = Host(aurora, builder => builder.UseSetting("Ai:Traces:Redaction:UnredactedRoles", "Admin, Chef"));

        Exception failure = Assert.ThrowsAny<Exception>(() => misconfigured.Factory.Services);

        Assert.Contains("names 'Chef', which is not a staff role", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConciergeAnswer_MovesToAnotherProviderByConfigurationOnly_AndTheTraceShowsEveryProviderTried()
    {
        // Anthropic, unreachable here, then the scripted Fake as its fallback: the same code, only configuration differs.
        await using StaffApi switched = Host(aurora, builder =>
        {
            builder.UseSetting("Inference:Providers:claude:Adapter", "Anthropic");
            builder.UseSetting("Inference:Providers:claude:Settings:BaseUrl", "https://127.0.0.1:9");
            builder.UseSetting("Inference:Providers:claude:Settings:ApiKeyVariable", "HOSPITALITYOS_TESTS_NO_SUCH_KEY");
            builder.UseSetting("Inference:Providers:claude:Settings:MaxRetries", "0");
            builder.UseSetting($"Inference:Tasks:{Task}:Provider", "claude");
            builder.UseSetting($"Inference:Tasks:{Task}:Model", "claude-sonnet-5-5");
            builder.UseSetting($"Inference:Tasks:{Task}:Fallback:0", "scripted:fake-concierge");
        });
        await aurora.SaveAsync(switched.Factory.Services, Cancellation);
        await aurora.IndexContentAsync(switched.Factory.Services, Cancellation);

        Guid id = await AskAsync(switched, "What time does the pool close tonight?");

        AITraceResponse trace = await GetAsync(switched, id, "Manager");
        Assert.Equal(AITraceOutcome.Answered, trace.Outcome);
        Assert.All(trace.Body.ModelCalls, call =>
        {
            Assert.Equal(["claude", "scripted"], call.Attempts.Select(attempt => attempt.Provider));
            Assert.Equal(("claude-sonnet-5-5", "concierge.answer"), (call.Attempts[0].Model, call.Attempts[0].Prompt?.Name));
            Assert.NotNull(call.Attempts[0].Failure);
            Assert.Null(call.Attempts[1].Failure);
        });
    }

    private static StaffApi Host(AuroraGrand aurora, Action<IWebHostBuilder> configure)
    {
        FakeScriptOptions concierge = new();
        concierge.Chat.Add(new FakeChatRuleOptions(
            WhenUserSays: "tomorrow",
            AfterTool: "Property_GetOperatingHours",
            Reply: $$"""{"status":"Answered","answer":"Tomorrow the pool closes at 21:00.","citations":[{"type":"OperatingHours","id":"{{aurora.PoolHours.Id.Value}}","version":2}]}"""));
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

        return new StaffApi(configure: builder =>
        {
            builder.UseSetting("Inference:Providers:scripted:Adapter", "Fake");
            builder.UseSetting("Inference:Providers:scripted:Settings:Script", "concierge");
            builder.UseSetting($"Inference:Tasks:{Task}:Provider", "scripted");
            builder.UseSetting($"Inference:Tasks:{Task}:Model", "fake-concierge");
            builder.ConfigureTestServices(services => services.Configure<FakeInferenceOptions>(options => options.Scripts["concierge"] = concierge));
            configure(builder);
        });
    }

    private Task<Guid> AskAsync(string question) => AskAsync(api, question);

    /// <summary>
    /// Answers a guest's question as the concierge will (H1): grounded answering, then the trace, committed together.
    /// </summary>
    private async Task<Guid> AskAsync(StaffApi host, string question)
    {
        await using AsyncServiceScope scope = host.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(aurora.TenantId);

        Result<GroundedAnswer> answer = await scope.ServiceProvider.GetRequiredService<IGroundedAnswerer>()
            .AnswerAsync(new GroundedQuestion(Task, aurora.PropertyId, [ChatMessage.User(question)], NoSource), Cancellation);
        Assert.True(answer.IsSuccess, answer.IsFailure ? answer.Error.Message : null);

        AITraceTrigger trigger = new(AITraceTrigger.GuestMessage, Guid.CreateVersion7(), question);
        AITraceId id = await scope.ServiceProvider.GetRequiredService<IAITraceRecorder>()
            .CompleteAsync(AITraceCompletion.For(trigger, aurora.PropertyId, answer.Value, "question"), Cancellation);
        Result<CommitReceipt> commit = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation);
        Assert.True(commit.IsSuccess, commit.IsFailure ? commit.Error.Message : null);

        return id.Value;
    }

    private async Task ChangePoolHoursAsync()
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(aurora.TenantId);
        OperatingHours hours = (await scope.ServiceProvider.GetRequiredService<IOperatingHoursRepository>().GetByIdAsync(aurora.PoolHours.Id, Cancellation))!;

        ValueList<DayHours> daily = ValueList.From(Enum.GetValues<DayOfWeek>().Select(day => new DayHours(day, [new TimeInterval(new TimeOnly(7, 0), new TimeOnly(21, 0))])));
        Assert.True(hours.Update(new OperatingHoursSchedule(daily, [], []), api.Clock.GetUtcNow()).IsSuccess);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation)).IsSuccess);
    }

    private Task<AITraceResponse> GetAsync(Guid id, string role) => GetAsync(api, id, role);

    private async Task<AITraceResponse> GetAsync(StaffApi host, Guid id, string role)
    {
        using HttpClient client = host.Staff(aurora.TenantId, role);

        return await StaffApi.OkAsync<AITraceResponse>(await client.GetAsync($"{Route}/{id}", Cancellation), Cancellation);
    }

    private async Task<AITracePage> ListAsync(string role, string query)
    {
        using HttpClient client = api.Staff(aurora.TenantId, role);

        return await StaffApi.OkAsync<AITracePage>(await client.GetAsync($"{Route}{query}", Cancellation), Cancellation);
    }
}
