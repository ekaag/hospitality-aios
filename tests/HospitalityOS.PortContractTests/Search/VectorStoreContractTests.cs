using HospitalityOS.Ports.Search;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.PortContractTests.Search;

/// <summary>
/// What every vector store must do: return the most similar records first (cosine similarity), none below the minimum
/// score; see only one tenant's records in one scope (P10); filter by kind; replace on upsert; and delete.
/// </summary>
public abstract class VectorStoreContractTests
{
    protected const string Collection = "content";
    protected const string Aurora = "aurora";

    private static readonly TenantId tenant = new(Guid.Parse("01890a5d-ac96-774b-bcce-0000005e0001"));
    private static readonly TenantId otherTenant = new(Guid.Parse("01890a5d-ac96-774b-bcce-0000005e0002"));

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_ReturnsTheMostSimilarFirst_AndNothingBelowTheMinimumScore()
    {
        IVectorStore store = CreateStore();
        await store.UpsertAsync(Collection, Record("east", 1, 0, 0), Cancellation);
        await store.UpsertAsync(Collection, Record("north-east", 1, 1, 0), Cancellation);
        await store.UpsertAsync(Collection, Record("up", 0, 0, 1), Cancellation);

        IReadOnlyList<SearchHit> hits = await store.SearchAsync(Collection, Query(2, 0.1f, 0, minScore: 0.5), Cancellation);

        Assert.Equal(["east", "north-east"], hits.Select(hit => hit.Id));
        Assert.InRange(hits[0].Score, 0.99, 1.0);
        Assert.InRange(hits[1].Score, 0.7, 0.75);
    }

    [Fact]
    public async Task Search_SeesOnlyItsTenantAndScope_AndFiltersByKind()
    {
        IVectorStore store = CreateStore();
        await store.UpsertAsync(Collection, Record("faq", 1, 0, 0), Cancellation);
        await store.UpsertAsync(Collection, Record("notice", 1, 0, 0) with { Kind = "Announcement" }, Cancellation);
        await store.UpsertAsync(Collection, Record("elsewhere", 1, 0, 0) with { Scope = "harbour" }, Cancellation);
        await store.UpsertAsync(Collection, Record("other-tenant", 1, 0, 0) with { TenantId = otherTenant }, Cancellation);
        await store.UpsertAsync("events", Record("event", 1, 0, 0), Cancellation);

        IReadOnlyList<SearchHit> all = await store.SearchAsync(Collection, Query(1, 0, 0), Cancellation);
        IReadOnlyList<SearchHit> faqs = await store.SearchAsync(Collection, Query(1, 0, 0) with { Kind = "Faq" }, Cancellation);

        Assert.Equal(["faq", "notice"], all.Select(hit => hit.Id).Order(StringComparer.Ordinal));
        Assert.Equal(["faq"], faqs.Select(hit => hit.Id));
    }

    [Fact]
    public async Task Upsert_ReplacesTheRecord_DeleteAndDeleteScopeRemove_AndTopLimits()
    {
        IVectorStore store = CreateStore();
        await store.UpsertAsync(Collection, Record("moved", 1, 0, 0), Cancellation);
        await store.UpsertAsync(Collection, Record("moved", 0, 1, 0), Cancellation);
        await store.UpsertAsync(Collection, Record("a", 1, 0, 0), Cancellation);
        await store.UpsertAsync(Collection, Record("b", 1, 0.1f, 0), Cancellation);
        await store.UpsertAsync(Collection, Record("kept", 1, 0, 0) with { TenantId = otherTenant }, Cancellation);

        IReadOnlyList<SearchHit> east = await store.SearchAsync(Collection, Query(1, 0, 0), Cancellation);
        IReadOnlyList<SearchHit> top = await store.SearchAsync(Collection, Query(1, 0, 0) with { Top = 1 }, Cancellation);
        await store.DeleteAsync(Collection, tenant, "a", Cancellation);
        IReadOnlyList<SearchHit> afterDelete = await store.SearchAsync(Collection, Query(1, 0, 0), Cancellation);
        await store.DeleteScopeAsync(Collection, tenant, Aurora, Cancellation);
        IReadOnlyList<SearchHit> afterScope = await store.SearchAsync(Collection, Query(1, 0, 0) with { MinScore = -1 }, Cancellation);
        IReadOnlyList<SearchHit> others = await store.SearchAsync(Collection, Query(1, 0, 0) with { TenantId = otherTenant }, Cancellation);

        Assert.Equal(["a", "b"], east.Select(hit => hit.Id));
        Assert.Equal(["a"], top.Select(hit => hit.Id));
        Assert.Equal(["b"], afterDelete.Select(hit => hit.Id));
        Assert.Empty(afterScope);
        Assert.Equal(["kept"], others.Select(hit => hit.Id));
    }

    protected abstract IVectorStore CreateStore();

    private static VectorRecord Record(string id, params float[] vector) => new(id, tenant, Aurora, "Faq", vector);

    private static VectorQuery Query(float x, float y, float z, double minScore = 0.5) => new(tenant, Aurora, new[] { x, y, z }, null, 10, minScore);
}
