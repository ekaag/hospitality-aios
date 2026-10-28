using System.ComponentModel;
using HospitalityOS.Ai;
using HospitalityOS.Ai.StructuredOutput;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Ai.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static HospitalityOS.UnitTests.Ai.TestDoubles.AiTestHost;

namespace HospitalityOS.UnitTests.Ai.StructuredOutput;

public sealed class TypedExtractorTests
{
    private const string Towels = """{"item":"towel","quantity":2,"urgency":"Normal","room":"412"}""";
    private const string TowelsMessage = "Can you send two towels to room 412?";

    private static readonly Dictionary<string, string> auroraGrand = new() { ["property"] = "Aurora Grand" };

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Extract_AsksForTheSchemaOfT_AndReadsTheOutputIntoT()
    {
        ScriptedModel model = new ScriptedModel().ThenExtract(Towels);
        using ServiceProvider services = Build(model);

        TypedExtraction<ItemRequest> extraction = (await ExtractAsync(services)).Value;

        Assert.Equal(new ItemRequest("towel", 2, Urgency.Normal, "412"), extraction.Value);
        ExtractionRequest request = Assert.Single(model.ExtractionRequests);
        Assert.Equal(
            ("Read the guest message at Aurora Grand and extract the service request.\n", TowelsMessage, nameof(ItemRequest), ContractSchemas.For(typeof(ItemRequest)).GetRawText()),
            (request.Instructions, request.Input, request.SchemaName, request.OutputSchema.GetRawText()));
        Assert.Equal(["test.extract@1"], extraction.Attempts.Select(attempt => attempt.Prompt?.ToString()));
    }

    [Theory]
    [InlineData("""{"item":"towel","quantity":2,"urgency":"Normal"}""")]
    [InlineData("""{"item":"towel","quantity":"two","urgency":"Normal","room":"412"}""")]
    [InlineData("""{"item":"towel","quantity":2,"urgency":"Whenever","room":"412"}""")]
    [InlineData("""{"item":"towel","quantity":2,"urgency":"Normal","room":"412","colour":"white"}""")]
    [InlineData("""{"item":null,"quantity":2,"urgency":"Normal","room":"412"}""")]
    [InlineData("null")]
    public async Task Extract_InvalidOutput_IsSentBackOnceWithWhatWasWrong_AndTheCorrectedOutputIsUsed(string invalid)
    {
        ScriptedModel model = new ScriptedModel().ThenExtract(invalid).ThenExtract(Towels);
        using ServiceProvider services = Build(model);

        TypedExtraction<ItemRequest> extraction = (await ExtractAsync(services)).Value;

        Assert.Equal(new ItemRequest("towel", 2, Urgency.Normal, "412"), extraction.Value);
        Assert.Equal(2, model.ExtractionRequests.Count);
        Assert.StartsWith(
            "Read the guest message at Aurora Grand and extract the service request.\n\nYour previous output did not match the required schema: ",
            model.ExtractionRequests[1].Instructions,
            StringComparison.Ordinal);
        Assert.EndsWith("\nProduce output that matches the schema exactly.", model.ExtractionRequests[1].Instructions, StringComparison.Ordinal);
        Assert.Equal((2, new InferenceUsage(40, 8)), (extraction.Attempts.Count, extraction.Usage));
    }

    [Fact]
    public async Task Extract_StillInvalidAfterTheCorrection_IsInvalidOutput_WithoutAThirdCall()
    {
        ScriptedModel model = new ScriptedModel().ThenExtract("{}").ThenExtract("""{"item":"towel"}""").ThenExtract(Towels);
        using ServiceProvider services = Build(model);

        Result<TypedExtraction<ItemRequest>> result = await ExtractAsync(services);

        Assert.Equal((AiErrors.InvalidOutputCode, ErrorKind.Unavailable), (result.Error.Code, result.Error.Kind));
        Assert.Equal(2, model.ExtractionRequests.Count);
    }

    private static ServiceProvider Build(ScriptedModel model) =>
        AiTestHost.Build(model, new ExecutorScript(), _ => { });

    private static async Task<Result<TypedExtraction<ItemRequest>>> ExtractAsync(ServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ITypedExtractor>().ExtractAsync<ItemRequest>(Extract, TowelsMessage, auroraGrand, Cancellation);
    }

    internal enum Urgency
    {
        Normal,
        Urgent,
    }

    internal sealed record ItemRequest(
        [property: Description("What to bring, singular, e.g. towel.")] string Item,
        int Quantity,
        Urgency Urgency,
        string? Room);
}
