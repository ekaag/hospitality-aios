using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;
using HospitalityOS.Adapters.Anthropic;

namespace HospitalityOS.UnitTests.Adapters.Anthropic;

/// <summary>
/// The Messages API as recorded responses: the real SDK sends its requests here, they are kept for inspection, and the
/// queued responses (or failures) come back in order. No network, no key, no cost.
/// </summary>
internal sealed class RecordedAnthropicApi : HttpMessageHandler
{
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> responses = new();

    public List<(HttpRequestMessage Request, JsonDocument Body)> Requests { get; } = [];

    /// <summary>
    /// The request body of the last call.
    /// </summary>
    public JsonElement LastBody => Requests[^1].Body.RootElement;

    public static string Message(string stopReason, string content, string model = "claude-sonnet-5-5") =>
        $$$"""
        {"id":"msg_01","type":"message","role":"assistant","model":"{{{model}}}","content":[{{{content}}}],
         "stop_reason":"{{{stopReason}}}","stop_sequence":null,
         "usage":{"input_tokens":120,"output_tokens":45,"cache_creation_input_tokens":10,"cache_read_input_tokens":30}}
        """;

    public static string Error(string type, string message) =>
        $$$"""{"type":"error","error":{"type":"{{{type}}}","message":"{{{message}}}"}}""";

    public RecordedAnthropicApi Returns(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        responses.Enqueue(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

        return this;
    }

    public RecordedAnthropicApi Fails(Exception exception)
    {
        responses.Enqueue(_ => Task.FromException<HttpResponseMessage>(exception));

        return this;
    }

    /// <summary>
    /// Never answers until the request is cancelled (by the client's timeout or the caller).
    /// </summary>
    public RecordedAnthropicApi Hangs()
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
    public AnthropicModel Model(bool refusalFallback = true, TimeSpan? timeout = null)
    {
        IAnthropicClient client = new AnthropicClient().WithOptions(options => options with
        {
            HttpClient = new HttpClient(this),
            ApiKey = "test-key",
            MaxRetries = 0,
            Timeout = timeout ?? TimeSpan.FromMinutes(1),
        });

        return new AnthropicModel(client, new AnthropicSettings(null, null, 0, timeout, refusalFallback));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, JsonDocument.Parse(body)));

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
