using System.Text.Json;
using System.Text.RegularExpressions;
using HospitalityOS.Adapters.Azure.Search;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Adapters.Azure;

/// <summary>
/// The Azure AI Search adapters' own logic, which needs no service (their contract tests run against a real one when
/// configured): guest text as plain words, keys, tenant filters, cosine scores, and index definitions that declare the
/// fields the adapters read and filter on.
/// </summary>
public sealed partial class AzureSearchTests
{
    private static readonly TenantId aurora = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209900001"));
    private static readonly TenantId harbour = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209900002"));

    [Theory]
    [InlineData("What is the wifi?", "What is the wifi")]
    [InlineData("Wi-Fi password", "WiFi password")]
    [InlineData("pool +spa -gym", "pool  spa gym")]
    [InlineData("\"late checkout\" (please)", "late checkout   please")]
    [InlineData("???", "")]
    public void GuestText_IsSearchedAsPlainWords(string text, string expected)
    {
        Assert.Equal(expected, AzureSearchIndex.Words(text));
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.5, 0.0)]
    [InlineData(1 / 3.0, -1.0)]
    public void AzureCosineScores_AreGivenBackAsSimilarity(double score, double similarity)
    {
        Assert.Equal(similarity, AzureVectorStore.Cosine(score), precision: 9);
    }

    [Fact]
    public void Keys_AreUniquePerTenantAndId_InTheCharactersKeysAllow()
    {
        string[] keys =
        [
            AzureSearchIndexes.KeyOf(aurora, "wifi"),
            AzureSearchIndexes.KeyOf(harbour, "wifi"),
            AzureSearchIndexes.KeyOf(aurora, "wifi/guest"),
            AzureSearchIndexes.KeyOf(aurora, "Wi-Fi ünïcode"),
        ];

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.Matches(KeyCharacters(), key));
    }

    [Fact]
    public void Filters_NameTheTenantAndScope_AndQuoteWhatTheyAreGiven()
    {
        Assert.Equal(
            "tenantId eq '01890a5d-ac96-774b-bcce-b30209900001' and scope eq 'o''brien' and kind eq 'Faq'",
            AzureSearchIndexes.Filter(aurora, "o'brien", "Faq"));
        Assert.Equal("tenantId eq '01890a5d-ac96-774b-bcce-b30209900001' and scope eq 'aurora'", AzureSearchIndexes.Filter(aurora, "aurora", null));
    }

    [Theory]
    [InlineData("content.json", new[] { "key", "tenantId", "scope", "kind", "id", "title", "body", "tags" })]
    [InlineData("content-vectors.json", new[] { "key", "tenantId", "scope", "kind", "id", "vector" })]
    public void IndexDefinitions_DeclareTheAdaptersFields_FilteringOnTenantScopeAndKind(string file, string[] fields)
    {
        JsonElement[] declared = [.. Definition(file).GetProperty("fields").EnumerateArray()];
        Dictionary<string, JsonElement> byName = declared.ToDictionary(field => field.GetProperty("name").GetString()!);

        Assert.Equal(fields, byName.Keys);
        Assert.True(byName[AzureSearchIndexes.Key].GetProperty("key").GetBoolean());
        Assert.All(
            new[] { AzureSearchIndexes.Tenant, AzureSearchIndexes.Scope, AzureSearchIndexes.Kind },
            name => Assert.True(byName[name].GetProperty("filterable").GetBoolean(), $"{name} must be filterable."));
        Assert.True(byName[AzureSearchIndexes.Id].GetProperty("retrievable").GetBoolean());
    }

    [Fact]
    public void TheVectorIndex_IsNamedForItsCollection_AndRanksByCosine()
    {
        JsonElement definition = Definition("content-vectors.json");

        Assert.Equal("content" + AzureSearchIndexes.VectorSuffix, definition.GetProperty("name").GetString());
        Assert.Equal("cosine", definition.GetProperty("vectorSearch").GetProperty("algorithms")[0].GetProperty("hnswParameters").GetProperty("metric").GetString());
    }

    private static JsonElement Definition(string file)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HospitalityOS.slnx")))
        {
            directory = directory.Parent;
        }

        string path = Path.Combine(directory!.FullName, "deploy", "azure", "search", file);

        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex KeyCharacters();
}
