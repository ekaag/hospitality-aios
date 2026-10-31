using HospitalityOS.Ai;
using HospitalityOS.Ai.Classifying;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Ai.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.UnitTests.Ai.Classifying;

public sealed class LabelClassifierTests
{
    private static readonly string[] labels = ["question", "service_request"];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Classify_SendsThePromptAndLabels_AndReturnsTheLabelChosen()
    {
        ScriptedModel model = new ScriptedModel().ThenClassify("service_request");
        using ServiceProvider services = AiTestHost.Build(model, new ExecutorScript(), _ => { });

        Result<string> label = await ClassifyAsync(services, "Can you send two towels?");

        Assert.Equal("service_request", label.Value);
        ClassificationRequest request = Assert.Single(model.ClassificationRequests);
        Assert.Equal(
            ("Triage the guest message.\n\nquestion: something to be answered.\nservice_request: something to be done or brought.\n", "Can you send two towels?"),
            (request.Instructions, request.Input));
        Assert.Equal(labels, request.Labels);
    }

    [Fact]
    public async Task Classify_ALabelThatWasNotOffered_IsRefused()
    {
        ScriptedModel model = new ScriptedModel().ThenClassify("complaint");
        using ServiceProvider services = AiTestHost.Build(model, new ExecutorScript(), _ => { });

        Result<string> label = await ClassifyAsync(services, "This is outrageous.");

        Assert.Equal(AiErrors.UnknownLabelCode, label.Error.Code);
    }

    private static async Task<Result<string>> ClassifyAsync(ServiceProvider services, string input)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ILabelClassifier>().ClassifyAsync(AiTestHost.Classify, input, labels, Cancellation);
    }
}
