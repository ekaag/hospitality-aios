using HospitalityOS.Ports.Inference;

namespace HospitalityOS.PortContractTests.Inference;

// The Anthropic adapter against Claude itself, on the models the implementation plan binds each kind of task to.
// Skipped unless ANTHROPIC_API_KEY is set; the adapter's request and response mapping is also tested offline against
// recorded API responses (UnitTests: Adapters/Anthropic). Anthropic serves no embedding or reranking models.

public sealed class AnthropicChatModelContractTests : ChatModelContractTests
{
    protected override ModelSelection Model { get; } = new("claude-sonnet-5-5", new InferenceParameters(Effort: InferenceEffort.Low));

    protected override IChatModel CreateModel() => InferenceAdapters.Anthropic<IChatModel>();
}

public sealed class AnthropicClassifierContractTests : ClassifierContractTests
{
    protected override ModelSelection Model { get; } = new("claude-haiku-5-5", new InferenceParameters(Effort: InferenceEffort.Low));

    protected override IClassifier CreateModel() => InferenceAdapters.Anthropic<IClassifier>();
}

public sealed class AnthropicStructuredExtractorContractTests : StructuredExtractorContractTests
{
    protected override ModelSelection Model { get; } = new("claude-haiku-5-5", new InferenceParameters(Effort: InferenceEffort.Low));

    protected override IStructuredExtractor CreateModel() => InferenceAdapters.Anthropic<IStructuredExtractor>();
}

public sealed class AnthropicEvaluationJudgeContractTests : EvaluationJudgeContractTests
{
    protected override ModelSelection Model { get; } = new("claude-opus-5-5", new InferenceParameters(Effort: InferenceEffort.Medium));

    protected override IEvaluationJudge CreateModel() => InferenceAdapters.Anthropic<IEvaluationJudge>();
}
