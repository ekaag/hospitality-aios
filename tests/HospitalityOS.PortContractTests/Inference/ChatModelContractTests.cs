using System.Text.Json;
using HospitalityOS.Ports.Inference;
using Xunit;
using static HospitalityOS.PortContractTests.Inference.InferenceContractData;

namespace HospitalityOS.PortContractTests.Inference;

/// <summary>
/// What every <see cref="IChatModel"/> adapter must do: answer as the assistant, report its model and usage, stop for
/// tool calls exactly when it makes them (naming only offered tools, with object arguments), take tool results back,
/// and honour cancellation.
/// </summary>
public abstract class ChatModelContractTests
{
    private const string Concierge = "You are a hotel concierge. Use tools for facts.";

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// The model and parameters to call the adapter with.
    /// </summary>
    protected abstract ModelSelection Model { get; }

    protected abstract IChatModel CreateModel();

    [Fact]
    public async Task Complete_AnswersAsTheAssistant_WithItsModelAndUsage()
    {
        ChatCompletion completion = await CreateModel().CompleteAsync(
            new ChatRequest("Answer in one short sentence.", [ChatMessage.User("Please say hello to Alex.")]), Model, Cancellation);

        Assert.Equal((ChatRole.Assistant, ChatStopReason.Completed), (completion.Message.Role, completion.StopReason));
        Assert.False(string.IsNullOrWhiteSpace(completion.Message.Text));
        Assert.DoesNotContain(completion.Message.Content, part => part is ChatToolCall);
        Assert.False(string.IsNullOrWhiteSpace(completion.Model));
        Assert.True(completion.Usage.InputTokens >= 0 && completion.Usage.OutputTokens >= 0);
    }

    [Fact]
    public async Task Complete_AskedSomethingOnlyAToolKnows_CallsTheTool_AndStopsForItsResult()
    {
        ChatCompletion completion = await CreateModel().CompleteAsync(
            new ChatRequest(Concierge, [ChatMessage.User("What time is it at the hotel right now? Use get_local_time.")], [LocalTimeTool]),
            Model,
            Cancellation);

        ChatToolCall[] calls = [.. completion.Message.Content.OfType<ChatToolCall>()];
        Assert.NotEmpty(calls);
        Assert.Equal(ChatStopReason.ToolCalls, completion.StopReason);
        Assert.All(calls, call =>
        {
            Assert.Equal(LocalTimeTool.Name, call.Name);
            Assert.False(string.IsNullOrWhiteSpace(call.CallId));
            Assert.Equal(JsonValueKind.Object, call.Arguments.ValueKind);
        });
    }

    [Fact]
    public async Task Complete_GivenTheResultOfItsOwnToolCall_AnswersFromIt()
    {
        IChatModel model = CreateModel();
        ChatMessage question = ChatMessage.User("What time is it at the hotel right now? Use get_local_time.");
        ChatCompletion asked = await model.CompleteAsync(new ChatRequest(Concierge, [question], [LocalTimeTool]), Model, Cancellation);
        ChatToolResult[] results = [.. asked.Message.Content.OfType<ChatToolCall>().Select(call => new ChatToolResult(call.CallId, """{"localTime":"21:40"}"""))];
        Assert.NotEmpty(results);

        // The model's message goes back exactly as received (with any provider reasoning), then the results.
        ChatCompletion answered = await model.CompleteAsync(
            new ChatRequest(Concierge, [question, asked.Message, new ChatMessage(ChatRole.Tool, [.. results])], [LocalTimeTool]),
            Model,
            Cancellation);

        Assert.Equal(ChatStopReason.Completed, answered.StopReason);
        Assert.True(
            answered.Message.Text.Contains("21:40", StringComparison.Ordinal) || answered.Message.Text.Contains("9:40", StringComparison.Ordinal),
            answered.Message.Text);
    }

    [Fact]
    public async Task Complete_WhenCancelled_Throws()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateModel().CompleteAsync(
            new ChatRequest(null, [ChatMessage.User("Hello")]), Model, cancelled.Token));
    }
}
