using HospitalityOS.Ports.Inference;

namespace HospitalityOS.PortContractTests.Inference;

// The Azure OpenAI adapter against a real Azure OpenAI (Foundry) resource, on the deployments the inference modules
// make. Skipped unless AZURE_OPENAI_ENDPOINT is set; the adapter's request and response mapping is also tested offline
// against recorded API responses (UnitTests: Adapters/AzureOpenAI). Azure OpenAI serves no reranking models.

public sealed class AzureOpenAIChatModelContractTests : ChatModelContractTests
{
    protected override ModelSelection Model { get; } = new("gpt-5-mini", new InferenceParameters(Effort: InferenceEffort.Low));

    protected override IChatModel CreateModel() => InferenceAdapters.AzureOpenAI<IChatModel>();
}

public sealed class AzureOpenAIClassifierContractTests : ClassifierContractTests
{
    protected override ModelSelection Model { get; } = new("gpt-5-mini", new InferenceParameters(Effort: InferenceEffort.Low));

    protected override IClassifier CreateModel() => InferenceAdapters.AzureOpenAI<IClassifier>();
}

public sealed class AzureOpenAIStructuredExtractorContractTests : StructuredExtractorContractTests
{
    protected override ModelSelection Model { get; } = new("gpt-5-mini", new InferenceParameters(Effort: InferenceEffort.Low));

    protected override IStructuredExtractor CreateModel() => InferenceAdapters.AzureOpenAI<IStructuredExtractor>();
}

public sealed class AzureOpenAIEmbeddingModelContractTests : EmbeddingModelContractTests
{
    protected override ModelSelection Model { get; } = new("text-embedding-3-small", InferenceParameters.Default);

    protected override IEmbeddingModel CreateModel() => InferenceAdapters.AzureOpenAI<IEmbeddingModel>();
}

public sealed class AzureOpenAIEvaluationJudgeContractTests : EvaluationJudgeContractTests
{
    protected override ModelSelection Model { get; } = new("gpt-5-mini", new InferenceParameters(Effort: InferenceEffort.Medium));

    protected override IEvaluationJudge CreateModel() => InferenceAdapters.AzureOpenAI<IEvaluationJudge>();
}
