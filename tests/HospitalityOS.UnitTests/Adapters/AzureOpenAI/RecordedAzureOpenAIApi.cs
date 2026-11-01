using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using HospitalityOS.Adapters.AzureOpenAI;

namespace HospitalityOS.UnitTests.Adapters.AzureOpenAI;

/// <summary>
/// An Azure OpenAI resource's v1 API as recorded responses: the real SDK sends its requests here, they are kept for
/// inspection, and the queued responses (or failures) come back in order. No network, no sign-in, no cost.
/// </summary>
internal sealed class RecordedAzureOpenAIApi : HttpMessageHandler
{
    public const string Endpoint = "https://inference-chat-dev-uksouth-oai.openai.azure.com/";

    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> responses = new();

    public List<(HttpRequestMessage Request, JsonDocument Body)> Requests { get; } = [];

    /// <summary>
    /// The request body of the last call.
    /// </summary>
    public JsonElement LastBody => Requests[^1].Body.RootElement;

    public static string Completion(string finishReason, string message, string model = "gpt-5-mini-2025-08-07") =>
        $$$"""
        {"id":"chatcmpl-1","object":"chat.completion","created":1761900000,"model":"{{{model}}}",
         "choices":[{"index":0,"finish_reason":"{{{finishReason}}}","message":{"role":"assistant",{{{message}}}}}],
         "usage":{"prompt_tokens":120,"completion_tokens":45,"total_tokens":165}}
        """;

    public static string Text(string text) => $"\"content\":{JsonSerializer.Serialize(text)}";

    public static string Error(string code, string message) =>
        $$$"""{"error":{"code":"{{{code}}}","message":"{{{message}}}"}}""";

    public RecordedAzureOpenAIApi Returns(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        responses.Enqueue(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

        return this;
    }

    public RecordedAzureOpenAIApi Fails(Exception exception)
    {
        responses.Enqueue(_ => Task.FromException<HttpResponseMessage>(exception));

        return this;
    }

    /// <summary>
    /// Never answers until the request is cancelled (by the client's timeout or the caller).
    /// </summary>
    public RecordedAzureOpenAIApi Hangs()
    {
        responses.Enqueue(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        return this;
    }

    /// <summary>
    /// The adapter over this recorded API, with no retries so failures surface at once.
    /// </summary>
    public AzureOpenAIModel Model(TimeSpan? timeout = null) =>
        new(AzureOpenAIModelFactory<object>.Client(
            new AzureOpenAISettings(new Uri(Endpoint), 0, timeout ?? TimeSpan.FromMinutes(1)),
            ApiKeyAuthenticationPolicy.CreateBearerAuthorizationPolicy(new ApiKeyCredential("test-token")),
            new HttpClientPipelineTransport(new HttpClient(this))));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, JsonDocument.Parse(body.Length == 0 ? "{}" : body)));

        return await responses.Dequeue()(cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        foreach ((_, JsonDocument body) in Requests)
        {
            body.Dispose();
        }

        base.Dispose(disposing);
    }
}
