using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.PortContractTests.Inference.Reference;

/// <summary>
/// A deterministic implementation of every inference port, written only to show the contract suites can be met and to
/// prove they catch violations; it is not an adapter. Word overlap stands in for understanding: it calls a tool the
/// user names, picks the label whose description shares most words with the input, embeds and reranks by shared words,
/// and passes a judged response when it repeats the reference's figures.
/// </summary>
internal sealed class ReferenceModel : IChatModel, IClassifier, IStructuredExtractor, IEmbeddingModel, IReranker, IEvaluationJudge
{
    private const int Dimensions = 64;

    private static readonly HashSet<string> stopWords = new(["the", "and", "for", "you", "are", "with"], StringComparer.Ordinal);

    private static readonly Dictionary<string, int> numberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
    };

    public Task<ChatCompletion> CompleteAsync(ChatRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        ChatMessage last = request.Messages[^1];
        ChatCompletion completion;

        if (last.Role == ChatRole.Tool)
        {
            string results = string.Join(" ", last.Content.OfType<ChatToolResult>().Select(result => result.Content));
            completion = Answer($"From the tool: {results}", model);
        }
        else if (request.Tools?.FirstOrDefault(tool => last.Text.Contains(tool.Name, StringComparison.Ordinal)) is { } tool)
        {
            ChatToolCall call = new($"call-{request.Messages.Count}", tool.Name, JsonDocument.Parse("{}").RootElement.Clone());
            completion = new ChatCompletion(new ChatMessage(ChatRole.Assistant, [call]), ChatStopReason.ToolCalls, Usage(last.Text, tool.Name), model.Model);
        }
        else
        {
            completion = Answer($"Hello! You said: {last.Text}", model);
        }

        return Task.FromResult(completion);

        ChatCompletion Answer(string text, ModelSelection selection) =>
            new(ChatMessage.Assistant(text), ChatStopReason.Completed, Usage(last.Text, text), selection.Model);
    }

    public Task<Classification> ClassifyAsync(ClassificationRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        // Instructions describe each label on a line "label: description".
        Dictionary<string, HashSet<string>> descriptions = request.Instructions
            .Split('\n')
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2 && request.Labels.Contains(parts[0].Trim()))
            .ToDictionary(parts => parts[0].Trim(), parts => Words(parts[1]), StringComparer.Ordinal);
        HashSet<string> input = Words(request.Input);
        string label = request.Labels.OrderByDescending(candidate => descriptions.GetValueOrDefault(candidate)?.Count(input.Contains) ?? 0).First();

        return Task.FromResult(new Classification(label, null, Usage(request.Input, label), model.Model));
    }

    public Task<Extraction> ExtractAsync(ExtractionRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        JsonObject output = [];

        foreach (JsonProperty property in request.OutputSchema.GetProperty("properties").EnumerateObject())
        {
            output[property.Name] = property.Value.GetProperty("type").GetString() switch
            {
                "integer" => FirstNumber(request.Input),
                "boolean" => false,
                _ => request.Input,
            };
        }

        return Task.FromResult(new Extraction(JsonSerializer.SerializeToElement(output), Usage(request.Input, output.ToJsonString()), model.Model));
    }

    public Task<Embeddings> EmbedAsync(EmbeddingRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        List<ReadOnlyMemory<float>> vectors = [];

        foreach (string input in request.Inputs)
        {
            float[] vector = new float[Dimensions];

            foreach (string word in Words(input))
            {
                vector[Bucket(word)] += 1;
            }

            vectors.Add(vector);
        }

        return Task.FromResult(new Embeddings(vectors, Usage(string.Concat(request.Inputs), ""), model.Model));
    }

    public Task<Reranking> RerankAsync(RerankRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        HashSet<string> query = Words(request.Query);
        IEnumerable<RankedDocument> ranked = request.Documents
            .Select((document, index) => new RankedDocument(index, Words(document).Count(query.Contains)))
            .OrderByDescending(document => document.Score)
            .ThenBy(document => document.Index)
            .Take(request.TopN ?? request.Documents.Count);

        return Task.FromResult(new Reranking(ValueList.From(ranked), Usage(request.Query, ""), model.Model));
    }

    public Task<Judgement> JudgeAsync(JudgeRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        // The reference's figures (times, prices) must appear in the response.
        string[] figures = [.. (request.Reference ?? "").Split(' ').Select(word => word.Trim('.', ',')).Where(word => word.Any(char.IsAsciiDigit))];
        double score = figures.Length == 0 ? 1 : figures.Count(figure => request.Output.Contains(figure, StringComparison.Ordinal)) / (double)figures.Length;
        string reasoning = figures.Length == 0 ? "No reference figures to check." : $"States {score:P0} of the reference's figures ({string.Join(", ", figures)}).";

        return Task.FromResult(new Judgement(score, score >= 1, reasoning, Usage(request.Output, reasoning), model.Model));
    }

    private static HashSet<string> Words(string text) =>
        [.. text.ToLowerInvariant()
            .Split([' ', ',', '.', '?', '!', ':', ';', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length > 3 && word.EndsWith('s') ? word[..^1] : word)
            .Where(word => word.Length >= 3 && !stopWords.Contains(word))];

    private static int FirstNumber(string text)
    {
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || numberWords.TryGetValue(word, out number))
            {
                return number;
            }
        }

        return 1;
    }

    // A stable hash (string.GetHashCode is randomized per process).
    private static int Bucket(string word)
    {
        uint hash = 2166136261;

        foreach (char character in word)
        {
            hash = (hash ^ character) * 16777619;
        }

        return (int)(hash % Dimensions);
    }

    private static InferenceUsage Usage(string input, string output) =>
        new(input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, output.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
}
