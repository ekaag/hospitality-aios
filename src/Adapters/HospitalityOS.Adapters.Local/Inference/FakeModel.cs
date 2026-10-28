using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Adapters.Local.Inference;

/// <summary>
/// A deterministic model for tests, evaluation runs, and development without a provider account. A script (if any)
/// answers first; otherwise word overlap stands in for understanding: it calls a tool the user names, answers from tool
/// results, picks the label whose description shares most words with the input, embeds and reranks by shared words,
/// fills an extraction schema from the input, and passes a judged response that repeats the reference's figures.
/// With <paramref name="unavailable"/> every call fails as an outage, to exercise fallbacks.
/// </summary>
internal sealed class FakeModel(FakeScriptOptions? script, bool unavailable)
    : IChatModel, IClassifier, IStructuredExtractor, IEmbeddingModel, IReranker, IEvaluationJudge
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
        Begin(cancellationToken);

        ChatMessage last = request.Messages[^1];
        string user = request.Messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? "";
        string toolResult = string.Join(" ", last.Content.OfType<ChatToolResult>().Select(result => result.Content));
        FakeChatRuleOptions? rule = script?.Chat.FirstOrDefault(candidate => Matches(candidate, request, user));

        ChatCompletion completion = rule switch
        {
            { CallTool: { } tool } => ToolCall(tool, rule.Arguments ?? "{}", request, model),
            { Reply: { } reply } => Answer(reply.Replace("{user}", user, StringComparison.Ordinal).Replace("{toolResult}", toolResult, StringComparison.Ordinal), user, model),
            _ when last.Role == ChatRole.Tool => Answer($"From the tool: {toolResult}", user, model),
            _ when request.Tools?.FirstOrDefault(tool => last.Text.Contains(tool.Name, StringComparison.Ordinal)) is { } tool => ToolCall(tool.Name, "{}", request, model),
            _ => Answer($"Hello! You said: {last.Text}", user, model),
        };

        return Task.FromResult(completion);
    }

    public Task<Classification> ClassifyAsync(ClassificationRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        Begin(cancellationToken);

        string? scripted = script?.Classification.FirstOrDefault(rule => Contains(request.Input, rule.WhenInputContains))?.Label;

        // Instructions describe each label on a line "label: description".
        Dictionary<string, HashSet<string>> descriptions = request.Instructions
            .Split('\n')
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2 && request.Labels.Contains(parts[0].Trim()))
            .ToDictionary(parts => parts[0].Trim(), parts => Words(parts[1]), StringComparer.Ordinal);
        HashSet<string> input = Words(request.Input);
        string label = scripted ?? request.Labels.OrderByDescending(candidate => descriptions.GetValueOrDefault(candidate)?.Count(input.Contains) ?? 0).First();

        return Task.FromResult(new Classification(label, null, Usage(request.Input, label), model.Model));
    }

    public Task<Extraction> ExtractAsync(ExtractionRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        Begin(cancellationToken);

        if (script?.Extraction.FirstOrDefault(rule => Contains(request.Input, rule.WhenInputContains)) is { } rule)
        {
            return Task.FromResult(new Extraction(Parse(rule.Output), Usage(request.Input, rule.Output), model.Model));
        }

        JsonObject output = [];

        if (request.OutputSchema.TryGetProperty("properties", out JsonElement properties))
        {
            foreach (JsonProperty property in properties.EnumerateObject())
            {
                output[property.Name] = TypeOf(property.Value) switch
                {
                    "integer" or "number" => FirstNumber(request.Input),
                    "boolean" => false,
                    _ => request.Input,
                };
            }
        }

        return Task.FromResult(new Extraction(JsonSerializer.SerializeToElement(output), Usage(request.Input, output.ToJsonString()), model.Model));
    }

    public Task<Embeddings> EmbedAsync(EmbeddingRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        Begin(cancellationToken);

        int dimensions = request.Dimensions ?? Dimensions;
        List<ReadOnlyMemory<float>> vectors = [];

        foreach (string input in request.Inputs)
        {
            float[] vector = new float[dimensions];

            foreach (string word in Words(input))
            {
                vector[Bucket(word, dimensions)] += 1;
            }

            vectors.Add(vector);
        }

        return Task.FromResult(new Embeddings(vectors, Usage(string.Join(" ", request.Inputs), ""), model.Model));
    }

    public Task<Reranking> RerankAsync(RerankRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        Begin(cancellationToken);

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
        Begin(cancellationToken);

        if (script?.Judgement.FirstOrDefault(rule => Contains(request.Output, rule.WhenOutputContains)) is { } rule)
        {
            return Task.FromResult(new Judgement(rule.Score, rule.Passed, rule.Reasoning, Usage(request.Output, rule.Reasoning), model.Model));
        }

        // The reference's figures (times, prices) must appear in the response.
        string[] figures = [.. (request.Reference ?? "").Split(' ').Select(word => word.Trim('.', ',')).Where(word => word.Any(char.IsAsciiDigit))];
        double score = figures.Length == 0 ? 1 : figures.Count(figure => request.Output.Contains(figure, StringComparison.Ordinal)) / (double)figures.Length;
        string reasoning = figures.Length == 0
            ? "No reference figures to check."
            : $"States {score.ToString("P0", CultureInfo.InvariantCulture)} of the reference's figures ({string.Join(", ", figures)}).";

        return Task.FromResult(new Judgement(score, score >= 1, reasoning, Usage(request.Output, reasoning), model.Model));
    }

    private void Begin(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (unavailable)
        {
            throw new InferenceUnavailableException("The fake provider is configured to be unavailable.");
        }
    }

    private static bool Matches(FakeChatRuleOptions rule, ChatRequest request, string user)
    {
        if (rule.WhenUserSays is { } said && !Contains(user, said))
        {
            return false;
        }

        if (rule.AfterTool is { } tool)
        {
            ChatMessage last = request.Messages[^1];
            HashSet<string> answered = [.. last.Content.OfType<ChatToolResult>().Select(result => result.CallId)];
            bool afterTool = last.Role == ChatRole.Tool && request.Messages
                .SelectMany(message => message.Content.OfType<ChatToolCall>())
                .Any(call => answered.Contains(call.CallId) && call.Name == tool);

            if (!afterTool)
            {
                return false;
            }
        }

        return true;
    }

    private static ChatCompletion ToolCall(string tool, string arguments, ChatRequest request, ModelSelection model)
    {
        ChatToolCall call = new($"fake-call-{request.Messages.Count}", tool, Parse(arguments));

        return new ChatCompletion(new ChatMessage(ChatRole.Assistant, [call]), ChatStopReason.ToolCalls, Usage(request.Messages[^1].Text, tool), model.Model);
    }

    private static ChatCompletion Answer(string text, string user, ModelSelection model) =>
        new(ChatMessage.Assistant(text), ChatStopReason.Completed, Usage(user, text), model.Model);

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    private static bool Contains(string text, string fragment) => text.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    // A schema's "type" is a name, or for a nullable member an array of names such as ["string", "null"].
    private static string? TypeOf(JsonElement schema) =>
        !schema.TryGetProperty("type", out JsonElement type) ? null
        : type.ValueKind is JsonValueKind.Array ? type.EnumerateArray().Select(name => name.GetString()).FirstOrDefault(name => name != "null")
        : type.GetString();

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

    // A stable hash (string.GetHashCode is randomized per process), so vectors are the same in every run.
    private static int Bucket(string word, int dimensions)
    {
        uint hash = 2166136261;

        foreach (char character in word)
        {
            hash = (hash ^ character) * 16777619;
        }

        return (int)(hash % (uint)dimensions);
    }

    private static InferenceUsage Usage(string input, string output) =>
        new(input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, output.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
}
