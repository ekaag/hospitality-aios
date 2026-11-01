using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;
using HospitalityOS.Adapters.Azure;
using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.PortContractTests.Adapters.Azure.Search;

/// <summary>
/// The Azure AI Search service <see cref="EndpointVariable"/> names (there is no emulator), as whoever is signed in to the
/// Azure CLI, who needs Search Service Contributor and Search Index Data Contributor there. Each test class gets indexes
/// of its own, made from the definitions the deployment applies (<c>deploy/azure/search/</c>) under a prefix, with the
/// vector width the contract tests use, and deletes them afterwards; a Basic or higher tier is needed, since Free allows
/// three indexes. Without the variable, the tests are skipped, CI included.
/// </summary>
internal sealed class AzureSearchTestService
{
    /// <summary>
    /// The service's URL, e.g. <c>https://search-content-dev-uksouth-srch.search.windows.net</c>.
    /// </summary>
    public const string EndpointVariable = "HOSPITALITYOS_SEARCH";

    private const string ApiVersion = "2025-09-01";

    /// <summary>
    /// How long a write takes to become searchable, which the in-process adapters do not need (near real time).
    /// </summary>
    private static readonly TimeSpan settle = TimeSpan.FromSeconds(2);

    private static readonly string[] indexes = ["content", "events"];

    private readonly string? endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
    private readonly TokenCredential credential = new DefaultAzureCredential();
    private readonly string prefix = $"t{Guid.NewGuid():N}"[..13] + "-";
    private bool created;

    public static TimeSpan Settle => settle;

    public bool Configured => !string.IsNullOrWhiteSpace(endpoint);

    public void SkipUnlessConfigured() =>
        Assert.SkipWhen(!Configured, $"Set {EndpointVariable} to an Azure AI Search service (Basic or higher) to run the Azure AI Search contract tests.");

    /// <summary>
    /// The adapter <typeparamref name="TPort"/> on this class's indexes.
    /// </summary>
    public TPort Adapter<TPort>()
        where TPort : class
    {
        ServiceProvider services = new ServiceCollection().AddAzureAdapters().BuildServiceProvider();
        IConfiguration settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Endpoint"] = endpoint, ["Prefix"] = prefix })
            .Build();

        return services.GetServices<IAdapterFactory<TPort>>().Single(candidate => candidate.AdapterName == "AzureAISearch").Create(services, settings);
    }

    public async Task CreateAsync(CancellationToken cancellationToken)
    {
        if (!Configured)
        {
            return;
        }

        using HttpClient http = await HttpAsync(cancellationToken);

        foreach (string index in indexes)
        {
            await PutAsync(http, Definition("content.json", prefix + index, vectorWidth: null), cancellationToken);
            await PutAsync(http, Definition("content-vectors.json", prefix + index + "-vectors", vectorWidth: 3), cancellationToken);
        }

        created = true;
    }

    public async Task DeleteAsync()
    {
        if (!created)
        {
            return;
        }

        using HttpClient http = await HttpAsync(CancellationToken.None);

        foreach (string index in indexes.SelectMany(index => new[] { prefix + index, prefix + index + "-vectors" }))
        {
            using HttpResponseMessage response = await http.DeleteAsync($"indexes/{index}?api-version={ApiVersion}");
        }
    }

    private static async Task PutAsync(HttpClient http, JsonObject definition, CancellationToken cancellationToken)
    {
        using StringContent body = new(definition.ToJsonString(), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await http.PutAsync($"indexes/{definition["name"]}?api-version={ApiVersion}", body, cancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"Creating {definition["name"]}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}");
    }

    private static JsonObject Definition(string file, string name, int? vectorWidth)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HospitalityOS.slnx")))
        {
            directory = directory.Parent;
        }

        JsonObject definition = JsonNode.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "deploy", "azure", "search", file)))!.AsObject();
        definition["name"] = name;

        if (vectorWidth is { } width)
        {
            definition["fields"]!.AsArray().Single(field => (string?)field!["name"] == "vector")!["dimensions"] = width;
        }

        return definition;
    }

    private async Task<HttpClient> HttpAsync(CancellationToken cancellationToken)
    {
        AccessToken token = await credential.GetTokenAsync(new TokenRequestContext(["https://search.azure.com/.default"]), cancellationToken);
        HttpClient http = new() { BaseAddress = new Uri(endpoint!.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        return http;
    }
}
