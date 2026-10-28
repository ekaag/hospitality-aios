using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.UnitTests.Bindings.Inference.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static HospitalityOS.UnitTests.Bindings.Inference.TestDoubles.InferenceTestHost;

namespace HospitalityOS.UnitTests.Bindings.Inference;

public sealed class InferenceRouterTests
{
    private static readonly TenantId auroraGrand = new(Guid.Parse("aa000000-0000-4000-8000-000000000001"));
    private static readonly TenantId other = new(Guid.Parse("bb000000-0000-4000-8000-000000000001"));
    private static readonly ChatRequest question = new("You are a concierge.", [ChatMessage.User("When does the pool close?")]);
    private static readonly string[] toSecondary = ["secondary:backup-model"];
    private static readonly string[] toSecondaryThenTertiary = ["secondary:backup-model", "tertiary:last-resort"];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Run_CallsTheTasksProvider_WithItsModelAndParameters()
    {
        using ServiceProvider services = Build(Settings(
            Provider("primary"),
            Route(Answer, "primary", "big-model"),
            [new("Inference:Tasks:concierge.answer:Parameters:Temperature", "0.2"), new("Inference:Tasks:concierge.answer:Parameters:MaxOutputTokens", "800")]));

        InferenceOutcome<ChatCompletion> outcome = await AnswerAsync(services);

        Assert.Equal("primary:big-model", outcome.Value.Message.Text);
        Assert.Equal(("primary", "big-model", new InferenceParameters(0.2, 800)), Assert.Single(services.GetRequiredService<ModelCalls>().Calls));
        Assert.Equal((Answer, "primary", "big-model", true), (outcome.ServedBy.Task, outcome.ServedBy.Provider, outcome.ServedBy.Model, outcome.ServedBy.Succeeded));
    }

    [Theory]
    [InlineData("aa000000-0000-4000-8000-000000000001", "tenant-provider:tenant-model")]
    [InlineData("bb000000-0000-4000-8000-000000000001", "primary:big-model")]
    [InlineData(null, "primary:big-model")]
    public async Task Run_UsesTheTenantsOverride_ElseTheDefault(string? tenant, string answeredBy)
    {
        using ServiceProvider services = Build(Settings(
            Provider("primary"),
            Provider("tenant-provider"),
            Route(Answer, "primary", "big-model"),
            Route(Answer, "tenant-provider", "tenant-model", tenant: auroraGrand.Value.ToString().ToUpperInvariant())));

        InferenceOutcome<ChatCompletion> outcome = await AnswerAsync(services, tenant is null ? null : new TenantId(Guid.Parse(tenant)));

        Assert.Equal(answeredBy, outcome.Value.Message.Text);
    }

    [Fact]
    public async Task Run_WhenProvidersAreUnavailable_FallsBackInOrder_AndRecordsEachAttempt()
    {
        using ServiceProvider services = Build(Settings(
            Provider("primary", "Test", ("Behavior", "unavailable"), ("Takes", "00:00:02")),
            Provider("secondary", "ChatOnly", ("Behavior", "unavailable")),
            Provider("tertiary", "Test", ("Takes", "00:00:00.300")),
            Route(Answer, "primary", "big-model", toSecondaryThenTertiary)));

        InferenceOutcome<ChatCompletion> outcome = await AnswerAsync(services, other);

        Assert.Equal("tertiary:last-resort", outcome.Value.Message.Text);
        Assert.Equal(
            [
                new InferenceAttempt(Answer, "primary", "big-model", TimeSpan.FromSeconds(2), "primary is overloaded"),
                new InferenceAttempt(Answer, "secondary", "backup-model", TimeSpan.Zero, "secondary is overloaded"),
                new InferenceAttempt(Answer, "tertiary", "last-resort", TimeSpan.FromMilliseconds(300)),
            ],
            outcome.Attempts);
    }

    [Fact]
    public async Task Run_WhenEveryProviderIsUnavailable_IsInferenceUnavailable_WithTheAttempts()
    {
        using ServiceProvider services = Build(Settings(
            Provider("primary", "Test", ("Behavior", "unavailable")),
            Provider("secondary", "Test", ("Behavior", "unavailable")),
            Route(Answer, "primary", "big-model", toSecondary)));

        Result<InferenceOutcome<ChatCompletion>> result = await RunAsync(services, null);

        InferenceUnavailableError error = Assert.IsType<InferenceUnavailableError>(result.Error);
        Assert.Equal((InferenceUnavailableError.UnavailableCode, ErrorKind.Unavailable, Answer), (error.Code, error.Kind, error.Task));
        Assert.Equal(["primary", "secondary"], error.Attempts.Select(attempt => attempt.Provider));
        Assert.All(error.Attempts, attempt => Assert.False(attempt.Succeeded));
    }

    [Fact]
    public async Task Run_WhenAProviderFailsOtherwise_DoesNotFallBack()
    {
        using ServiceProvider services = Build(Settings(
            Provider("primary", "Test", ("Behavior", "broken")),
            Provider("secondary"),
            Route(Answer, "primary", "big-model", toSecondary)));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(services, null));

        Assert.Equal("primary rejected the request", exception.Message);
        Assert.Equal(["primary"], services.GetRequiredService<ModelCalls>().Calls.Select(call => call.Label));
    }

    [Fact]
    public async Task Run_WhenCancelled_DoesNotFallBack()
    {
        using ServiceProvider services = Build(Settings(Provider("primary"), Provider("secondary"), Route(Answer, "primary", "big-model", toSecondary)));
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider.GetRequiredService<IInferenceRouter>()
            .RunAsync<IChatModel, ChatCompletion>(Answer, (chat, model, token) => chat.CompleteAsync(question, model, token), cancelled.Token));

        Assert.Empty(services.GetRequiredService<ModelCalls>().Calls);
    }

    [Fact]
    public async Task Run_AnUndeclaredTask_OrADeclaredTaskThroughAnotherPort_Throws()
    {
        using ServiceProvider services = Build(Settings(Provider("primary"), Route(Answer, "primary", "big-model"), Route(Classify, "primary", "small-model")));
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IInferenceRouter router = scope.ServiceProvider.GetRequiredService<IInferenceRouter>();

        InvalidOperationException undeclared = await Assert.ThrowsAsync<InvalidOperationException>(() => router.RunAsync<IChatModel, ChatCompletion>(
            "concierge.improvise", (chat, model, token) => chat.CompleteAsync(question, model, token), Cancellation));
        InvalidOperationException wrongPort = await Assert.ThrowsAsync<InvalidOperationException>(() => router.RunAsync<IChatModel, ChatCompletion>(
            Classify, (chat, model, token) => chat.CompleteAsync(question, model, token), Cancellation));

        Assert.Contains("is not declared", undeclared.Message, StringComparison.Ordinal);
        Assert.Contains("is declared for IClassifier, not IChatModel", wrongPort.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_CreatesOneInstancePerProviderAndPort_AndReusesIt()
    {
        using ServiceProvider services = Build(Settings(Provider("primary"), Route(Answer, "primary", "big-model"), Route(Classify, "primary", "small-model")));

        await AnswerAsync(services);
        await AnswerAsync(services, auroraGrand);
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            Result<InferenceOutcome<Classification>> classified = await scope.ServiceProvider.GetRequiredService<IInferenceRouter>().RunAsync<IClassifier, Classification>(
                Classify, (classifier, model, token) => classifier.ClassifyAsync(new ClassificationRequest("Route it.", "Towels please", ["request", "question"]), model, token), Cancellation);
            Assert.Equal("request", classified.Value.Value.Label);
        }

        Assert.Equal(["primary/IChatModel", "primary/IClassifier"], services.GetRequiredService<ModelCalls>().Created.Order(StringComparer.Ordinal));
    }

    private static async Task<InferenceOutcome<ChatCompletion>> AnswerAsync(ServiceProvider services, TenantId? tenant = null)
    {
        Result<InferenceOutcome<ChatCompletion>> result = await RunAsync(services, tenant);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        return result.Value;
    }

    private static async Task<Result<InferenceOutcome<ChatCompletion>>> RunAsync(ServiceProvider services, TenantId? tenant)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        if (tenant is { } id)
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(id);
        }

        return await scope.ServiceProvider.GetRequiredService<IInferenceRouter>()
            .RunAsync<IChatModel, ChatCompletion>(Answer, (chat, model, token) => chat.CompleteAsync(question, model, token), Cancellation);
    }
}
