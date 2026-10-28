using HospitalityOS.Ports.Inference;

namespace HospitalityOS.PortContractTests.Inference;

// The deterministic Fake adapter (Adapters.Local) against every inference contract.

public sealed class FakeChatModelContractTests : ChatModelContractTests
{
    protected override ModelSelection Model { get; } = new("fake", InferenceParameters.Default);

    protected override IChatModel CreateModel() => InferenceAdapters.Fake<IChatModel>();
}

public sealed class FakeClassifierContractTests : ClassifierContractTests
{
    protected override ModelSelection Model { get; } = new("fake", InferenceParameters.Default);

    protected override IClassifier CreateModel() => InferenceAdapters.Fake<IClassifier>();
}

public sealed class FakeStructuredExtractorContractTests : StructuredExtractorContractTests
{
    protected override ModelSelection Model { get; } = new("fake", InferenceParameters.Default);

    protected override IStructuredExtractor CreateModel() => InferenceAdapters.Fake<IStructuredExtractor>();
}

public sealed class FakeEmbeddingModelContractTests : EmbeddingModelContractTests
{
    protected override ModelSelection Model { get; } = new("fake", InferenceParameters.Default);

    protected override IEmbeddingModel CreateModel() => InferenceAdapters.Fake<IEmbeddingModel>();
}

public sealed class FakeRerankerContractTests : RerankerContractTests
{
    protected override ModelSelection Model { get; } = new("fake", InferenceParameters.Default);

    protected override IReranker CreateModel() => InferenceAdapters.Fake<IReranker>();
}

public sealed class FakeEvaluationJudgeContractTests : EvaluationJudgeContractTests
{
    protected override ModelSelection Model { get; } = new("fake", InferenceParameters.Default);

    protected override IEvaluationJudge CreateModel() => InferenceAdapters.Fake<IEvaluationJudge>();
}
