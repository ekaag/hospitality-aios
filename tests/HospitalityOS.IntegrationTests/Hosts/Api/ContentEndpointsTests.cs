using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Content;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// <c>/api/v1/content</c> through the real host: an administrator drafts, edits, publishes, and archives descriptive
/// content; only published content reaches the concierge; other tenants can change nothing.
/// </summary>
public sealed class ContentEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/content";

    private static readonly string[] wifiTags = [" WiFi ", "internet", "wifi"];
    private static readonly string[] blankTag = ["wifi", " "];

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Content events index asynchronously: ask again until the answer settles, for at most ten seconds.
    private async Task<SearchContentOutput> SearchUntilAsync(StaffApi host, object question, Func<IReadOnlyList<ContentEntry>, bool> settled)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            SearchContentOutput output = await host.AskAsGuestAsync<SearchContentOutput>(tenant, "Property.SearchContent", question, timeout.Token);

            if (settled(output.Results))
            {
                return output;
            }

            host.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Lifecycle_DraftPublishArchiveRepublish_EmitsEachChange_AndNormalizesTags()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);

        ContentItemResponse draft = await CreateAsync(admin, property);
        ContentItemResponse published = await StaffApi.OkAsync<ContentItemResponse>(await StaffApi.SendAsync(admin, HttpMethod.Post, $"{Route}/{draft.Id}/publish", null, "\"1\"", Cancellation), Cancellation);
        ContentItemResponse archived = await StaffApi.OkAsync<ContentItemResponse>(await admin.PostAsync($"{Route}/{draft.Id}/archive", null, Cancellation), Cancellation);
        ContentItemResponse republished = await StaffApi.OkAsync<ContentItemResponse>(await admin.PostAsync($"{Route}/{draft.Id}/publish", null, Cancellation), Cancellation);

        Assert.Equal(["internet", "wifi"], draft.Tags);
        Assert.Equal(
            [(ContentStatus.Draft, 1L), (ContentStatus.Published, 2L), (ContentStatus.Archived, 3L), (ContentStatus.Published, 4L)],
            new[] { draft, published, archived, republished }.Select(item => (item.Status, item.Version)));
        Assert.Equal(3, (await api.EventsAsync(tenant, Cancellation)).Count(message => message.EventType == "ContentItemUpdated"));
    }

    [Fact]
    public async Task OnlyPublishedContent_ReachesTheConcierge_OnceTheIndexHasCaughtUp()
    {
        await using StaffApi dispatching = new(dispatchEvents: true);
        PropertyResponse property = await dispatching.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = dispatching.Staff(tenant);
        ContentItemResponse draft = await CreateAsync(admin, property);
        object question = new { propertyId = property.Id.Value, query = "wifi" };

        SearchContentOutput whileDraft = await dispatching.AskAsGuestAsync<SearchContentOutput>(tenant, "Property.SearchContent", question, Cancellation);
        using HttpResponseMessage publish = await admin.PostAsync($"{Route}/{draft.Id}/publish", null, Cancellation);
        SearchContentOutput whilePublished = await SearchUntilAsync(dispatching, question, results => results.Count == 1);
        using HttpResponseMessage archive = await admin.PostAsync($"{Route}/{draft.Id}/archive", null, Cancellation);
        SearchContentOutput whileArchived = await SearchUntilAsync(dispatching, question, results => results.Count == 0);

        Assert.Empty(whileDraft.Results);
        Assert.Equal((draft.Id, 2L), (whilePublished.Results[0].Source.Id, whilePublished.Results[0].Source.Version));
        Assert.Empty(whileArchived.Results);
    }

    [Fact]
    public async Task Update_ReplacesTheText_KeepsTheStatus_AndAStaleVersionIs412()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        ContentItemResponse draft = await CreateAsync(admin, property);

        using HttpResponseMessage updated = await StaffApi.SendAsync(
            admin, HttpMethod.Put, $"{Route}/{draft.Id}", new { type = "Faq", title = "How do I get online?", body = "Join Aurora-Guest; no password." }, "\"1\"", Cancellation);
        using HttpResponseMessage stale = await StaffApi.SendAsync(admin, HttpMethod.Post, $"{Route}/{draft.Id}/publish", null, "\"1\"", Cancellation);

        ContentItemResponse current = await StaffApi.OkAsync<ContentItemResponse>(updated, Cancellation);
        Assert.Equal(("How do I get online?", ContentStatus.Draft, 0, 2L), (current.Title, current.Status, current.Tags.Count, current.Version));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
    }

    [Fact]
    public async Task Create_WithMissingAndInvalidFields_ReportsEveryField()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { propertyId = property.Id.Value, title = "Wi-Fi", tags = blankTag, about = new { id = Guid.CreateVersion7() } }, Cancellation);

        Assert.Equal(
            ["content.body_required", "content.tag_required", "content.type_required", "content.about_kind_required"],
            await StaffApi.FieldCodesAsync(response, Cancellation));
    }

    [Fact]
    public async Task AnotherTenant_CanNeitherReadNorPublishTheContent()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient owner = api.Staff(tenant);
        ContentItemResponse draft = await CreateAsync(owner, property);
        using HttpClient intruder = api.Staff(new TenantId(Guid.CreateVersion7()), "Admin", "mallory");

        using HttpResponseMessage read = await intruder.GetAsync($"{Route}/{draft.Id}", Cancellation);
        using HttpResponseMessage publish = await intruder.PostAsync($"{Route}/{draft.Id}/publish", null, Cancellation);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (read.StatusCode, publish.StatusCode));
        Assert.Equal(ContentStatus.Draft, (await owner.GetFromJsonAsync<ContentItemResponse>($"{Route}/{draft.Id}", ContractJson.Options, Cancellation))?.Status);
    }

    private static async Task<ContentItemResponse> CreateAsync(HttpClient admin, PropertyResponse property)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route,
            new { propertyId = property.Id.Value, type = "Faq", title = "Wi-Fi", body = "Join the Aurora-Guest network.", tags = wifiTags },
            Cancellation);

        return await StaffApi.CreatedAsync<ContentItemResponse>(response, Cancellation);
    }
}
