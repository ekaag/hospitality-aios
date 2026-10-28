using HospitalityOS.Ports.Search;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.PortContractTests.Search;

/// <summary>
/// What every keyword index must do: find documents by their words, best first, title and tag words counting more than
/// body words; see only one tenant's documents in one scope (P10); filter by kind; replace on upsert; and delete.
/// </summary>
public abstract class SearchIndexContractTests
{
    protected const string Index = "content";
    protected const string Aurora = "aurora";

    private static readonly TenantId tenant = new(Guid.Parse("01890a5d-ac96-774b-bcce-0000005e0001"));
    private static readonly TenantId otherTenant = new(Guid.Parse("01890a5d-ac96-774b-bcce-0000005e0002"));

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_FindsMatchingDocumentsBestFirst_TitleAndTagWordsCountingMore()
    {
        ISearchIndex index = CreateIndex();
        await index.UpsertAsync(Index, Document("parking", "Parking", "The car park has a fast wifi signal."), Cancellation);
        await index.UpsertAsync(Index, Document("wifi", "Wi-Fi", "Join the Aurora-Guest network.", "internet"), Cancellation);
        await index.UpsertAsync(Index, Document("spa", "Spa", "Treatments daily."), Cancellation);

        IReadOnlyList<SearchHit> wifi = await index.SearchAsync(Index, Query("What is the wifi?"), Cancellation);
        IReadOnlyList<SearchHit> internet = await index.SearchAsync(Index, Query("internet"), Cancellation);

        Assert.Equal(["wifi", "parking"], wifi.Select(hit => hit.Id));
        Assert.True(wifi[0].Score > wifi[1].Score);
        Assert.Equal(["wifi"], internet.Select(hit => hit.Id));
    }

    [Fact]
    public async Task Search_SeesOnlyItsTenantAndScope_AndFiltersByKind()
    {
        ISearchIndex index = CreateIndex();
        await index.UpsertAsync(Index, Document("faq", "Wifi", "Network name.", kind: "Faq"), Cancellation);
        await index.UpsertAsync(Index, Document("notice", "Wifi upgrade", "Faster soon.", kind: "Announcement"), Cancellation);
        await index.UpsertAsync(Index, Document("elsewhere", "Wifi", "Other hotel.") with { Scope = "harbour" }, Cancellation);
        await index.UpsertAsync(Index, Document("other-tenant", "Wifi", "Other tenant.") with { TenantId = otherTenant }, Cancellation);
        await index.UpsertAsync("events", Document("event", "Wifi talk", "In the ballroom."), Cancellation);

        IReadOnlyList<SearchHit> all = await index.SearchAsync(Index, Query("wifi"), Cancellation);
        IReadOnlyList<SearchHit> faqs = await index.SearchAsync(Index, Query("wifi") with { Kind = "Faq" }, Cancellation);

        Assert.Equal(["faq", "notice"], all.Select(hit => hit.Id).Order(StringComparer.Ordinal));
        Assert.Equal(["faq"], faqs.Select(hit => hit.Id));
    }

    [Fact]
    public async Task Upsert_ReplacesTheDocument_DeleteAndDeleteScopeRemove_AndTopLimits()
    {
        ISearchIndex index = CreateIndex();
        await index.UpsertAsync(Index, Document("wifi", "Wi-Fi", "Old network."), Cancellation);
        await index.UpsertAsync(Index, Document("wifi", "Internet", "New network."), Cancellation);
        await index.UpsertAsync(Index, Document("parking", "Parking", "Network of signs."), Cancellation);
        await index.UpsertAsync(Index, Document("pool", "Pool", "No network signal here."), Cancellation);
        await index.UpsertAsync(Index, Document("kept", "Network", "Other tenant.") with { TenantId = otherTenant }, Cancellation);

        IReadOnlyList<SearchHit> old = await index.SearchAsync(Index, Query("old"), Cancellation);
        IReadOnlyList<SearchHit> top = await index.SearchAsync(Index, Query("network") with { Top = 2 }, Cancellation);
        await index.DeleteAsync(Index, tenant, "parking", Cancellation);
        IReadOnlyList<SearchHit> afterDelete = await index.SearchAsync(Index, Query("network"), Cancellation);
        await index.DeleteScopeAsync(Index, tenant, Aurora, Cancellation);
        IReadOnlyList<SearchHit> afterScope = await index.SearchAsync(Index, Query("network"), Cancellation);
        IReadOnlyList<SearchHit> others = await index.SearchAsync(Index, Query("network") with { TenantId = otherTenant }, Cancellation);

        Assert.Empty(old);
        Assert.Equal(2, top.Count);
        Assert.Equal(["pool", "wifi"], afterDelete.Select(hit => hit.Id).Order(StringComparer.Ordinal));
        Assert.Empty(afterScope);
        Assert.Equal(["kept"], others.Select(hit => hit.Id));
    }

    protected abstract ISearchIndex CreateIndex();

    private static SearchDocument Document(string id, string title, string body, string? tag = null, string kind = "Faq") =>
        new(id, tenant, Aurora, kind, title, body, tag is null ? [] : [tag]);

    private static KeywordQuery Query(string text) => new(tenant, Aurora, text, null, 10);
}
