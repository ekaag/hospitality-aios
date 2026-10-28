using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Ai;
using HospitalityOS.Ai.ToolLoops;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.IntegrationTests.Capabilities.Reading;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HospitalityOS.IntegrationTests.Ai;

/// <summary>
/// A tool loop in the real host: the Fake model, scripted like a concierge, answers "What time does the pool close
/// tonight?" by calling <c>Property.GetOperatingHours</c> through the real capability executor against the Aurora Grand,
/// as the agent, and answering from the result.
/// </summary>
public sealed class ToolLoopHostTests : IAsyncLifetime
{
    private const string Task = "test.concierge";

    private readonly AuroraGrand aurora = new(new TenantId(Guid.CreateVersion7()));
    private readonly WebApplicationFactory<Program> factory;

    public ToolLoopHostTests()
    {
        FakeScriptOptions concierge = new();
        concierge.Chat.Add(new FakeChatRuleOptions(AfterTool: "Property_GetOperatingHours", Reply: "Here are the pool's hours: {toolResult}"));
        concierge.Chat.Add(new FakeChatRuleOptions(
            WhenUserSays: "pool",
            CallTool: "Property_GetOperatingHours",
            Arguments: $$"""{"propertyId":"{{aurora.PropertyId.Value}}","subject":"Amenity","subjectId":"{{aurora.Pool.Id.Value}}"}"""));

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Eventing:Dispatcher:Enabled", "false");
            builder.UseSetting("Inference:Providers:scripted:Adapter", "Fake");
            builder.UseSetting("Inference:Providers:scripted:Settings:Script", "concierge");
            builder.UseSetting($"Inference:Tasks:{Task}:Provider", "scripted");
            builder.UseSetting($"Inference:Tasks:{Task}:Model", "fake-concierge");
            builder.ConfigureTestServices(services => services
                .AddSingleton<TimeProvider>(new FakeTimeProvider(AuroraGrand.Now))
                .Configure<FakeInferenceOptions>(options => options.Scripts["concierge"] = concierge)
                .AddToolLoop(Task, null, ["Property.GetProperty@1", "Property.GetOperatingHours@1"]));
        });
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await aurora.SaveAsync(factory.Services, Cancellation);

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task PoolQuestion_IsAnsweredFromTheOperatingHoursRecord_ThroughTheCapabilityExecutor_AsTheAgent()
    {
        ToolLoopResult result;

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(aurora.TenantId);
            Result<ToolLoopResult> run = await scope.ServiceProvider.GetRequiredService<IToolLoop>()
                .RunAsync(new ToolLoopRequest(Task, [ChatMessage.User("What time does the pool close tonight?")]), Cancellation);
            Assert.True(run.IsSuccess, run.IsFailure ? run.Error.Message : null);
            result = run.Value;
        }

        Assert.Equal(ChatStopReason.Completed, result.StopReason);
        Assert.StartsWith("Here are the pool's hours: ", result.Answer.Text, StringComparison.Ordinal);
        Assert.Contains("2026-10-26T22:00:00", result.Answer.Text, StringComparison.Ordinal);
        Assert.Contains($"\"id\":\"{aurora.PoolHours.Id.Value}\",\"version\":1", result.Answer.Text, StringComparison.Ordinal);
        ToolCallRecord call = Assert.Single(result.ToolCalls);
        Assert.Equal(("Property.GetOperatingHours@1", (string?)null), (call.Capability, call.ErrorCode));
        Assert.Equal(["scripted", "scripted"], result.Attempts.Select(attempt => attempt.Provider));

        await using AsyncServiceScope reader = factory.Services.CreateAsyncScope();
        reader.ServiceProvider.GetRequiredService<TenantContext>().Set(aurora.TenantId);
        CapabilityInvocation? invocation = await reader.ServiceProvider.GetRequiredService<ICapabilityInvocationRepository>()
            .GetByIdAsync(new CapabilityInvocationId(call.InvocationId!.Value), Cancellation);
        Assert.Equal(
            ("Property.GetOperatingHours", CapabilityInvocationStatus.Succeeded, Actor.Agent(Task)),
            (invocation?.CapabilityName, invocation?.Status, invocation?.Actor));
    }
}
