using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The OpenAPI document is the frontends' contract (J1): the committed <c>apps/packages/api-client/openapi.json</c>, which
/// their client is generated from, must be what the API describes (set <see cref="WriteVariable"/> to rewrite it after an
/// API change), and the document must say enough to generate a typed client: every operation has an ID, a success
/// response with its body, and the problem it fails with; operations that need a caller name the bearer scheme; and IDs
/// are UUID strings.
/// </summary>
public sealed class OpenApiDocumentTests : IAsyncDisposable
{
    /// <summary>
    /// Set to <c>1</c> to write the API's document to the committed contract instead of comparing them.
    /// </summary>
    public const string WriteVariable = "HOSPITALITYOS_WRITE_OPENAPI";

    private static readonly JsonSerializerOptions indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // With dev tokens on, so the frontends' sign-in is in the contract too.
    private readonly StaffApi api = new(configure: builder => builder.UseSetting("Authentication:DevTokens:Enabled", "true"));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheDocument_IsTheCommittedContract()
    {
        string document = await DocumentTextAsync();
        string path = Path.Combine(RepositoryRoot(), "apps", "packages", "api-client", "openapi.json");

        if (Environment.GetEnvironmentVariable(WriteVariable) == "1")
        {
            await File.WriteAllTextAsync(path, document, Cancellation);
        }

        Assert.True(File.Exists(path), $"{path} is missing; run the tests with {WriteVariable}=1 to write it.");
        Assert.True(
            Normalize(await File.ReadAllTextAsync(path, Cancellation)) == document,
            $"The API's OpenAPI document differs from {path}. If the API change is intended, run the tests with {WriteVariable}=1 and regenerate the client (pnpm generate in apps/).");
    }

    [Fact]
    public async Task EveryOperation_HasAnId_ASuccessBody_AndItsProblem()
    {
        JsonNode document = JsonNode.Parse(await DocumentTextAsync())!;
        List<string> missing = [];

        foreach ((string path, string method, JsonObject operation) in Operations(document))
        {
            JsonObject responses = operation["responses"]!.AsObject();

            if (operation["operationId"] is null)
            {
                missing.Add($"{method} {path} has no operationId (WithName).");
            }

            if (!responses.Any(response => response.Key.StartsWith('2') && response.Value!["content"]?["application/json"]?["schema"] is not null))
            {
                missing.Add($"{method} {path} has no success response with a body (Produces).");
            }

            if (responses["default"]?["content"]?["application/problem+json"]?["schema"]?["$ref"]?.GetValue<string>() != "#/components/schemas/Problem")
            {
                missing.Add($"{method} {path} does not describe its problem.");
            }
        }

        Assert.Empty(missing);
        Assert.True(Operations(document).Count() >= 65);
    }

    [Theory]
    [InlineData("/api/v1/conversations", "post", true)]
    [InlineData("/api/v1/staff/{id}", "get", true)]
    [InlineData("/api/v1/ai/traces", "get", true)]
    [InlineData("/api/dev/tokens", "post", false)]
    [InlineData("/api/dev/tokens/personas", "get", false)]
    public async Task AnOperationThatNeedsACaller_NamesTheBearerScheme(string path, string method, bool needsCaller)
    {
        JsonNode document = JsonNode.Parse(await DocumentTextAsync())!;

        JsonNode? security = document["paths"]![path]![method]!["security"];

        Assert.Equal(needsCaller, security?[0]?["Bearer"] is not null);
    }

    [Fact]
    public async Task IdsAndLists_AreDescribedAsTheyAreSent()
    {
        JsonNode document = JsonNode.Parse(await DocumentTextAsync())!;
        JsonNode schemas = document["components"]!["schemas"]!;

        Assert.Equal(("string", "uuid"), (schemas["StaffMemberId"]!["type"]!.GetValue<string>(), schemas["StaffMemberId"]!["format"]!.GetValue<string>()));
        Assert.Equal("array", schemas["ValueListOfstring"]!["type"]!.GetValue<string>());
        Assert.Equal("uuid", document["paths"]!["/api/v1/staff/{id}"]!["get"]!["parameters"]![0]!["schema"]!["format"]!.GetValue<string>());
        Assert.Equal(["Guest", "Concierge", "Staff", "System"], schemas["MessageSender"]!["enum"]!.AsArray().Select(name => name!.GetValue<string>()));
    }

    public ValueTask DisposeAsync() => api.DisposeAsync();

    private static IEnumerable<(string Path, string Method, JsonObject Operation)> Operations(JsonNode document) =>
        document["paths"]!.AsObject().SelectMany(path => path.Value!.AsObject().Select(operation => (path.Key, operation.Key, operation.Value!.AsObject())));

    /// <summary>
    /// The API's document, indented, with LF line endings inside and out (descriptions come from XML comments, whose line
    /// endings follow the checkout).
    /// </summary>
    private async Task<string> DocumentTextAsync()
    {
        using HttpClient client = api.Client();

        return Normalize(await client.GetStringAsync("/openapi/v1.json", Cancellation));
    }

    private static string Normalize(string json) =>
        JsonSerializer.Serialize(JsonNode.Parse(json.Replace("\\r\\n", "\\n", StringComparison.Ordinal)), indented).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HospitalityOS.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (HospitalityOS.slnx) was not found.");
    }
}
