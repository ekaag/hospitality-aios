using System.Text.Json;
using HospitalityOS.Ports.Inference;
using Xunit;
using static HospitalityOS.PortContractTests.Inference.InferenceContractData;

namespace HospitalityOS.PortContractTests.Inference;

/// <summary>
/// What every <see cref="IClassifier"/> adapter must do: choose exactly one of the given labels (the right one for an
/// unambiguous message), with a confidence from 0 to 1 when it gives one.
/// </summary>
public abstract class ClassifierContractTests
{
    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    protected abstract ModelSelection Model { get; }

    protected abstract IClassifier CreateModel();

    [Theory]
    [InlineData("Could you bring two extra bath towels to room 412?", "service_request")]
    [InlineData("What time does the pool close tonight?", "question")]
    public async Task Classify_ChoosesTheRightLabel_FromThoseGiven(string message, string expected)
    {
        Classification classification = await CreateModel().ClassifyAsync(
            new ClassificationRequest(ClassifierInstructions, message, ["question", "service_request"]), Model, Cancellation);

        Assert.Equal(expected, classification.Label);
        Assert.True(classification.Confidence is null or (>= 0 and <= 1), $"Confidence {classification.Confidence}");
        Assert.False(string.IsNullOrWhiteSpace(classification.Model));
    }
}

/// <summary>
/// What every <see cref="IStructuredExtractor"/> adapter must do: return a JSON object holding every required property
/// of the schema, with the schema's types.
/// </summary>
public abstract class StructuredExtractorContractTests
{
    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    protected abstract ModelSelection Model { get; }

    protected abstract IStructuredExtractor CreateModel();

    [Fact]
    public async Task Extract_ReturnsAnObjectWithTheSchemasRequiredProperties()
    {
        Extraction extraction = await CreateModel().ExtractAsync(
            new ExtractionRequest("Extract what the guest asks for and how many.", "Two bath towels to room 412, please.", "towel_request", TowelRequestSchema),
            Model,
            Cancellation);

        Assert.Equal(JsonValueKind.Object, extraction.Output.ValueKind);
        Assert.Equal(JsonValueKind.String, extraction.Output.GetProperty("item").ValueKind);
        Assert.Equal(2, extraction.Output.GetProperty("quantity").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(extraction.Model));
    }
}

/// <summary>
/// What every <see cref="IEmbeddingModel"/> adapter must do: one vector per input, in order, all the same length, with
/// related texts closer together than unrelated ones (what retrieval relies on).
/// </summary>
public abstract class EmbeddingModelContractTests
{
    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    protected abstract ModelSelection Model { get; }

    protected abstract IEmbeddingModel CreateModel();

    [Fact]
    public async Task Embed_ReturnsOneVectorPerInput_PlacingRelatedTextsCloser()
    {
        Embeddings embeddings = await CreateModel().EmbedAsync(
            new EmbeddingRequest(["When does the swimming pool close?", "Pool opening hours: the pool closes at 22:00.", "Vegan dishes on the dinner menu."]),
            Model,
            Cancellation);

        Assert.Equal(3, embeddings.Vectors.Count);
        Assert.True(embeddings.Vectors[0].Length > 0);
        Assert.All(embeddings.Vectors, vector => Assert.Equal(embeddings.Vectors[0].Length, vector.Length));
        Assert.True(Cosine(embeddings.Vectors[0], embeddings.Vectors[1]) > Cosine(embeddings.Vectors[0], embeddings.Vectors[2]));
    }

    private static double Cosine(ReadOnlyMemory<float> left, ReadOnlyMemory<float> right)
    {
        double dot = 0, leftNorm = 0, rightNorm = 0;

        for (int i = 0; i < left.Length; i++)
        {
            dot += left.Span[i] * right.Span[i];
            leftNorm += left.Span[i] * left.Span[i];
            rightNorm += right.Span[i] * right.Span[i];
        }

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }
}

/// <summary>
/// What every <see cref="IReranker"/> adapter must do: rank documents by descending score, each at most once, at most
/// TopN of them, the most relevant first.
/// </summary>
public abstract class RerankerContractTests
{
    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    protected abstract ModelSelection Model { get; }

    protected abstract IReranker CreateModel();

    [Fact]
    public async Task Rerank_RanksTheRelevantDocumentFirst_WithinTopN()
    {
        Reranking reranking = await CreateModel().RerankAsync(
            new RerankRequest(
                "When does the pool close?",
                ["Vegan dishes on the dinner menu.", "The pool closes at 22:00 every day.", "Valet parking costs GBP 45 per night."],
                TopN: 2),
            Model,
            Cancellation);

        Assert.InRange(reranking.Documents.Count, 1, 2);
        Assert.Equal(1, reranking.Documents[0].Index);
        Assert.Equal(reranking.Documents.Count, reranking.Documents.Select(document => document.Index).Distinct().Count());
        Assert.All(reranking.Documents, document => Assert.InRange(document.Index, 0, 2));
        Assert.Equal(reranking.Documents.OrderByDescending(document => document.Score).Select(document => document.Index), reranking.Documents.Select(document => document.Index));
    }
}

/// <summary>
/// What every <see cref="IEvaluationJudge"/> adapter must do: score from 0 to 1 with reasons, passing a response that
/// meets the criteria and failing one that contradicts the reference.
/// </summary>
public abstract class EvaluationJudgeContractTests
{
    private const string Criteria = "The response states the pool's closing time exactly as the reference gives it and invents nothing.";

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    protected abstract ModelSelection Model { get; }

    protected abstract IEvaluationJudge CreateModel();

    [Theory]
    [InlineData("The pool closes at 22:00 tonight.", true)]
    [InlineData("The pool is open all night.", false)]
    public async Task Judge_PassesAResponseMeetingTheCriteria_AndFailsOneContradictingTheReference(string output, bool passes)
    {
        Judgement judgement = await CreateModel().JudgeAsync(
            new JudgeRequest(Criteria, "What time does the pool close tonight?", output, "The pool closes at 22:00."), Model, Cancellation);

        Assert.Equal(passes, judgement.Passed);
        Assert.InRange(judgement.Score, 0, 1);
        Assert.False(string.IsNullOrWhiteSpace(judgement.Reasoning));
    }
}
