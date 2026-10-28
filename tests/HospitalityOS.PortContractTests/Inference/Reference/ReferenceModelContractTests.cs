using HospitalityOS.Ports.Inference;

namespace HospitalityOS.PortContractTests.Inference.Reference;

// The inference suites run against the reference model until real adapters join them (G2: Fake and Anthropic).

public sealed class ReferenceChatModelContractTests : ChatModelContractTests
{
    protected override ModelSelection Model { get; } = new("reference", InferenceParameters.Default);

    protected override IChatModel CreateModel() => new ReferenceModel();
}

public sealed class ReferenceClassifierContractTests : ClassifierContractTests
{
    protected override ModelSelection Model { get; } = new("reference", InferenceParameters.Default);

    protected override IClassifier CreateModel() => new ReferenceModel();
}

public sealed class ReferenceStructuredExtractorContractTests : StructuredExtractorContractTests
{
    protected override ModelSelection Model { get; } = new("reference", InferenceParameters.Default);

    protected override IStructuredExtractor CreateModel() => new ReferenceModel();
}

public sealed class ReferenceEmbeddingModelContractTests : EmbeddingModelContractTests
{
    protected override ModelSelection Model { get; } = new("reference", InferenceParameters.Default);

    protected override IEmbeddingModel CreateModel() => new ReferenceModel();
}

public sealed class ReferenceRerankerContractTests : RerankerContractTests
{
    protected override ModelSelection Model { get; } = new("reference", InferenceParameters.Default);

    protected override IReranker CreateModel() => new ReferenceModel();
}

public sealed class ReferenceEvaluationJudgeContractTests : EvaluationJudgeContractTests
{
    protected override ModelSelection Model { get; } = new("reference", InferenceParameters.Default);

    protected override IEvaluationJudge CreateModel() => new ReferenceModel();
}
