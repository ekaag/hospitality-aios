using System.Text.Json;
using HospitalityOS.Ai;
using HospitalityOS.Ai.ToolLoops;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Ai.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static HospitalityOS.UnitTests.Ai.TestDoubles.AiTestHost;

namespace HospitalityOS.UnitTests.Ai.ToolLoops;

public sealed class ToolLoopTests
{
    private const string PoolArguments = """{"amenity":"pool"}""";

    private static readonly string[] hoursTool = ["Test.GetHours@1"];
    private static readonly Dictionary<string, string> auroraGrand = new() { ["property"] = "Aurora Grand" };
    private static readonly ToolLoopRequest poolQuestion = new(Concierge, [ChatMessage.User("When does the pool close?")], auroraGrand);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Run_OffersOnlyTheAllowListedCapabilities_AtTheirPinnedVersions_AsTools()
    {
        ScriptedModel model = new ScriptedModel().Then(ScriptedModel.Answer("The pool closes at 22:00."));
        using ServiceProvider services = Build(model, new ExecutorScript(), Declare(hoursTool));

        await RunAsync(services, poolQuestion);

        ChatTool tool = Assert.Single(model.ChatRequests[0].Tools!);
        Assert.Equal(("Test_GetHours", "When an amenity is open."), (tool.Name, tool.Description));
        Assert.Equal(AiTestHost.Capabilities[0].InputSchema.GetRawText(), tool.InputSchema.GetRawText());
        Assert.Equal("You are the concierge at Aurora Grand.\nAnswer the guest briefly, using only what the tools return, and name your source.\n", model.ChatRequests[0].SystemPrompt);
    }

    [Fact]
    public async Task Run_RunsTheToolsTheModelCalls_AsTheAgent_ThenAsksAgainWithTheirResults()
    {
        ChatCompletion asks = ScriptedModel.Calls(("call-1", "Test_GetHours", PoolArguments));
        ScriptedModel model = new ScriptedModel().Then(asks).Then(ScriptedModel.Answer("The pool closes at 22:00."));
        ExecutorScript executor = new();
        executor.Results.Enqueue(JsonSerializer.SerializeToElement(new { opens = "07:00", closes = "22:00" }));
        using ServiceProvider services = Build(model, executor, Declare(hoursTool));

        ToolLoopResult result = await RunAsync(services, poolQuestion);

        (CapabilityRequest request, Actor actor) = Assert.Single(executor.Calls);
        Assert.Equal(("Test.GetHours", (int?)1, PoolArguments, Actor.Agent(Concierge)), (request.Name, request.Version, request.Input.GetRawText(), actor));
        ChatMessage results = model.ChatRequests[1].Messages[^1];
        Assert.Equal(ChatRole.Tool, results.Role);
        Assert.Equal(new ChatToolResult("call-1", """{"opens":"07:00","closes":"22:00"}"""), Assert.Single(results.Content));
        Assert.Equal([poolQuestion.Messages[0], asks.Message, result.Added[1]], model.ChatRequests[1].Messages);
        Assert.Equal(asks.Message, result.Added[0]);
        Assert.Equal(("The pool closes at 22:00.", ChatStopReason.Completed, 3), (result.Answer.Text, result.StopReason, result.Added.Count));
        ToolCallRecord record = Assert.Single(result.ToolCalls);
        Assert.Equal(new ToolCallRecord("call-1", "Test_GetHours", "Test.GetHours@1", request.Input, RecordingExecutor.InvocationFor(1), null), record with { Output = null });
        Assert.Equal("""{"opens":"07:00","closes":"22:00"}""", record.Output?.GetRawText());
        Assert.Equal(["test.concierge@2", "test.concierge@2"], result.Attempts.Select(attempt => attempt.Prompt?.ToString()));
        Assert.Equal(new InferenceUsage(20, 10), result.Usage);
    }

    [Fact]
    public async Task Run_AToolThatIsNotAllowListed_IsRefusedWithoutRunning_EvenIfTheCapabilityExists()
    {
        ScriptedModel model = new ScriptedModel()
            .Then(ScriptedModel.Calls(("call-1", "Test_GetSecret", "{}"), ("call-2", "Test_GetHours_v2", PoolArguments), ("call-3", "Test.GetHours", PoolArguments)))
            .Then(ScriptedModel.Answer("I can't find that."));
        ExecutorScript executor = new();
        using ServiceProvider services = Build(model, executor, Declare(hoursTool));

        ToolLoopResult result = await RunAsync(services, poolQuestion);

        Assert.Empty(executor.Calls);
        Assert.All(model.ChatRequests[1].Messages[^1].Content.Cast<ChatToolResult>(), refused =>
        {
            Assert.True(refused.IsError);
            Assert.Equal("tool.unavailable", JsonDocument.Parse(refused.Content).RootElement.GetProperty("code").GetString());
        });
        Assert.All(result.ToolCalls, record => Assert.Equal((null, "tool.unavailable"), (record.Capability, record.ErrorCode)));
    }

    [Fact]
    public async Task Run_ACapabilityError_GoesBackToTheModelAsAToolError_SoItCanCorrectItself()
    {
        ScriptedModel model = new ScriptedModel()
            .Then(ScriptedModel.Calls(("call-1", "Test_GetHours", """{"amenty":"pool"}""")))
            .Then(ScriptedModel.Calls(("call-2", "Test_GetHours", PoolArguments)))
            .Then(ScriptedModel.Answer("The pool closes at 22:00."));
        ExecutorScript executor = new();
        executor.Results.Enqueue(new Error("capability.invalid_input", "The input does not match the capability's schema: 'amenty' is not a member.", ErrorKind.Validation));
        using ServiceProvider services = Build(model, executor, Declare(hoursTool));

        ToolLoopResult result = await RunAsync(services, poolQuestion);

        ChatToolResult error = Assert.IsType<ChatToolResult>(Assert.Single(model.ChatRequests[1].Messages[^1].Content));
        JsonElement content = JsonDocument.Parse(error.Content).RootElement;
        Assert.Equal(("call-1", true), (error.CallId, error.IsError));
        Assert.Equal(
            ("capability.invalid_input", "The input does not match the capability's schema: 'amenty' is not a member."),
            (content.GetProperty("code").GetString(), content.GetProperty("message").GetString()));
        Assert.Equal(["capability.invalid_input", null], result.ToolCalls.Select(record => record.ErrorCode));
        Assert.Equal(2, executor.Calls.Count);
        Assert.Equal("The pool closes at 22:00.", result.Answer.Text);
    }

    [Fact]
    public async Task Run_SeveralCallsInOneMessage_RunInOrder_AndAreAnsweredInOneToolMessage()
    {
        ScriptedModel model = new ScriptedModel()
            .Then(ScriptedModel.Calls(("call-1", "Test_GetHours", PoolArguments), ("call-2", "Test_GetHours", """{"amenity":"gym"}""")))
            .Then(ScriptedModel.Answer("Both are open."));
        ExecutorScript executor = new();
        using ServiceProvider services = Build(model, executor, Declare(hoursTool));

        await RunAsync(services, poolQuestion);

        Assert.Equal(["pool", "gym"], executor.Calls.Select(call => call.Request.Input.GetProperty("amenity").GetString()));
        Assert.Equal(["call-1", "call-2"], model.ChatRequests[1].Messages[^1].Content.Cast<ChatToolResult>().Select(result => result.CallId));
    }

    [Fact]
    public async Task Run_WhenTheModelKeepsCallingToolsPastTheRoundLimit_FailsWithoutRunningMore()
    {
        ScriptedModel model = new();

        for (int round = 1; round <= 3; round++)
        {
            model.Then(ScriptedModel.Calls(($"call-{round}", "Test_GetHours", PoolArguments)));
        }

        ExecutorScript executor = new();
        using ServiceProvider services = Build(model, executor, Declare(hoursTool, maxToolRounds: 2));

        Result<ToolLoopResult> result = await RunResultAsync(services, poolQuestion);

        Assert.Equal((AiErrors.ToolRoundLimitCode, ErrorKind.Unavailable), (result.Error.Code, result.Error.Kind));
        Assert.Equal((3, 2), (model.ChatRequests.Count, executor.Calls.Count));
    }

    [Fact]
    public async Task Run_WhenNoProviderCanServeARound_IsInferenceUnavailable()
    {
        ScriptedModel model = new ScriptedModel()
            .Then(ScriptedModel.Calls(("call-1", "Test_GetHours", PoolArguments)))
            .Then(_ => throw new InferenceUnavailableException("overloaded"));
        using ServiceProvider services = Build(model, new ExecutorScript(), Declare(hoursTool));

        Result<ToolLoopResult> result = await RunResultAsync(services, poolQuestion);

        Assert.Equal(InferenceUnavailableError.UnavailableCode, result.Error.Code);
    }

    [Theory]
    [InlineData(ChatStopReason.MaxTokens)]
    [InlineData(ChatStopReason.ContentFiltered)]
    public async Task Run_WhenTheModelStopsForAnotherReason_ReturnsItForTheCallerToHandle(ChatStopReason stop)
    {
        ScriptedModel model = new ScriptedModel().Then(ScriptedModel.Answer("The pool", stop));
        using ServiceProvider services = Build(model, new ExecutorScript(), Declare(hoursTool));

        ToolLoopResult result = await RunAsync(services, poolQuestion);

        Assert.Equal((stop, "The pool"), (result.StopReason, result.Answer.Text));
    }

    [Fact]
    public async Task Run_AnUndeclaredTask_PromptValuesForATaskWithoutAPrompt_OrAScopeActingAsSomeoneElse_Throw()
    {
        ScriptedModel model = new ScriptedModel().Then(ScriptedModel.Answer("Hello."));
        using ServiceProvider services = Build(model, new ExecutorScript(), services => services.AddToolLoop(Concierge, null, hoursTool));
        using ServiceProvider declared = Build(new ScriptedModel(), new ExecutorScript(), Declare(hoursTool));

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunResultAsync(services, poolQuestion with { Task = "test.improvise" }));
        await Assert.ThrowsAsync<ArgumentException>(() => RunResultAsync(services, poolQuestion));
        await using AsyncServiceScope scope = declared.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(Actor.Staff("ana", StaffRole.Manager));
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IToolLoop>().RunAsync(poolQuestion, Cancellation));
    }

    internal static Action<IServiceCollection> Declare(string[] tools, int maxToolRounds = HospitalityOS.Ai.DependencyInjection.DefaultMaxToolRounds) =>
        services => services.AddToolLoop(Concierge, "test.concierge", tools, maxToolRounds);

    private static async Task<ToolLoopResult> RunAsync(ServiceProvider services, ToolLoopRequest request)
    {
        Result<ToolLoopResult> result = await RunResultAsync(services, request);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        return result.Value;
    }

    private static async Task<Result<ToolLoopResult>> RunResultAsync(ServiceProvider services, ToolLoopRequest request)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IToolLoop>().RunAsync(request, Cancellation);
    }
}
