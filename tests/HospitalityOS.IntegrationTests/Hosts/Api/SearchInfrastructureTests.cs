using System.Text.Json;
using HospitalityOS.Capabilities.Retrieval;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The search indexes the deployment creates (<c>deploy/azure/search/</c>) fit the API as it is deployed: one keyword
/// index and one vector index per logical index the app writes, and the vector width is what <c>content.embed</c>
/// produces at the width the API asks for (<c>Search:Content:Dimensions</c>), since a vector index's width is fixed when
/// it is created.
/// </summary>
public sealed class SearchInfrastructureTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheContentVectorIndex_IsAsWideAsTheDeployedEmbeddings()
    {
        using WebApplicationFactory<Program> factory = new();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        int dimensions = scope.ServiceProvider.GetRequiredService<IOptions<ContentSearchOptions>>().Value.Dimensions;

        Result<InferenceOutcome<Embeddings>> embedded = await scope.ServiceProvider.GetRequiredService<IInferenceRouter>().RunAsync<IEmbeddingModel, Embeddings>(
            "content.embed",
            (model, selection, _, token) => model.EmbedAsync(new EmbeddingRequest(ValueList.From(["Wi-Fi: join Aurora-Guest."]), dimensions), selection, token),
            Cancellation);

        Assert.True(embedded.IsSuccess);
        Assert.Equal(VectorWidth("content-vectors.json"), embedded.Value.Value.Vectors[0].Length);
    }

    [Fact]
    public void EachLogicalIndex_HasAKeywordAndAVectorIndex()
    {
        string[] names = [.. Directory.GetFiles(SearchFolder(), "*.json").Select(path => Definition(Path.GetFileName(path)).GetProperty("name").GetString()!).Order(StringComparer.Ordinal)];

        Assert.Equal(["content", "content-vectors"], names);
    }

    private static int VectorWidth(string file) =>
        Definition(file).GetProperty("fields").EnumerateArray().Single(field => field.GetProperty("name").GetString() == "vector").GetProperty("dimensions").GetInt32();

    private static JsonElement Definition(string file) => JsonDocument.Parse(File.ReadAllText(Path.Combine(SearchFolder(), file))).RootElement.Clone();

    private static string SearchFolder()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HospitalityOS.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The repository root was not found."), "deploy", "azure", "search");
    }
}
