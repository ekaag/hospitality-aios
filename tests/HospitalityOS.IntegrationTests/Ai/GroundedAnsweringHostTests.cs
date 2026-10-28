using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Ai;
using HospitalityOS.Ai.Grounding;
using HospitalityOS.Bindings;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.IntegrationTests.Capabilities.Reading;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HospitalityOS.IntegrationTests.Ai;

/// <summary>
/// Grounded answering in the real host against the Aurora Grand, with the Fake model scripted like a concierge: the
/// pool question is answered from the OperatingHours record and cites it (Journey 5); an invented answer citing a
/// record nobody retrieved is refused, and the guest is told honestly that there is no confirmed information (Journey 17).
/// </summary>
public sealed class GroundedAnsweringHostTests : IAsyncLifetime
{
    private const string Task = "test.grounded";
    private const string NoSource = "I don't have confirmed information about that.";

    private static readonly Guid invented = Guid.Parse("01890a5d-ac96-774b-bcce-0000000dead1");

    private readonly AuroraGrand aurora = new(new TenantId(Guid.CreateVersion7()));
    private readonly WebApplicationFactory<Program> factory;

    public GroundedAnsweringHostTests()
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
                .AddPrompts(typeof(GroundedAnsweringHostTests).Assembly)
                .AddGroundedAnswering(Task, Task, ["Property.GetOperatingHours@1", "Property.GetPolicy@1"]));
        });
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await aurora.SaveAsync(factory.Services, Cancellation);
        await aurora.IndexContentAsync(factory.Services, Cancellation);
    }

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task PoolQuestion_IsAnsweredFromTheOperatingHoursRecord_AndCitesIt()
    {
        GroundedAnswer answer = await AskAsync("What time does the pool close tonight?");

        Assert.Equal((GroundedOutcome.Answered, "The pool closes at 22:00 tonight.", null), (answer.Outcome, answer.Text, answer.Rejection));
        SourceReference poolHours = new("OperatingHours", aurora.PoolHours.Id.Value, 1);
        Assert.Equal([poolHours], answer.Citations);
        Assert.Contains(poolHours, answer.Retrieved);
        Assert.Contains(new SourceReference("Property", aurora.PropertyId.Value, 1), answer.Retrieved);
        Assert.All(answer.Context, read => Assert.Null(read.ErrorCode));
        Assert.Equal("Property.GetOperatingHours@1", Assert.Single(answer.ToolCalls).Capability);
    }

    [Fact]
    public async Task AnInventedAnswer_CitingARecordNobodyRetrieved_BecomesAnHonestNoSourceAnswer()
    {
        GroundedAnswer answer = await AskAsync("Do you offer free helicopter transfers?");

        Assert.Equal((GroundedOutcome.NoAuthoritativeSource, NoSource), (answer.Outcome, answer.Text));
        Assert.Empty(answer.Citations);
        Assert.NotNull(answer.Rejection);
        Assert.DoesNotContain(answer.Retrieved, source => source.Id == invented);
    }

    private async Task<GroundedAnswer> AskAsync(string question)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(aurora.TenantId);

        Result<GroundedAnswer> result = await scope.ServiceProvider.GetRequiredService<IGroundedAnswerer>()
            .AnswerAsync(new GroundedQuestion(Task, aurora.PropertyId, [ChatMessage.User(question)], NoSource), Cancellation);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        return result.Value;
    }
}
