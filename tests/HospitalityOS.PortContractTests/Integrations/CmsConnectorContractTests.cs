using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Cms;
using HospitalityOS.SharedKernel.Ids;
using Xunit;
using static HospitalityOS.PortContractTests.Integrations.ConnectorTestData;

namespace HospitalityOS.PortContractTests.Integrations;

/// <summary>
/// What every <see cref="ICmsConnector"/> provider must do.
/// </summary>
public abstract class CmsConnectorContractTests
{
    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// The provider's connector for <paramref name="propertyId"/>, with the provider's system holding <paramref name="content"/>.
    /// </summary>
    protected abstract Task<ICmsConnector> CreateConnectorAsync(PropertyId propertyId, IReadOnlyList<CmsSeed> content);

    /// <summary>
    /// Changes an item's title in the provider's system.
    /// </summary>
    protected abstract Task RetitleAsync(ExternalId id, string title);

    [Fact]
    public async Task ListPublishedContent_ReturnsOnlyThePropertysPublishedItemsInCanonicalShape()
    {
        ICmsConnector connector = await CreateConnectorAsync(AuroraGrand, [
            new CmsSeed(AuroraGrand, ContentType.Faq, "Wi-Fi", "Join Aurora-Guest.", ["internet"], Published: true),
            new CmsSeed(AuroraGrand, ContentType.Directions, "Parking", "Use the Strand entrance.", [], Published: true),
            new CmsSeed(AuroraGrand, ContentType.Announcement, "Spa opening", "Draft.", [], Published: false),
            new CmsSeed(Harbourside, ContentType.Faq, "Wi-Fi", "Join Harbour-Guest.", [], Published: true),
        ]);

        IReadOnlyList<CmsContent> content = await connector.ListPublishedContentAsync(Cancellation);

        Assert.Equal(
            [("Parking", ContentType.Directions, "Use the Strand entrance.", ""), ("Wi-Fi", ContentType.Faq, "Join Aurora-Guest.", "internet")],
            content.Select(item => (item.Title, item.Type, item.Body, string.Join(",", item.Tags))).Order());
    }

    [Fact]
    public async Task ListPublishedContent_KeepsIdsAndRevisionsUntilAnItemChanges()
    {
        ICmsConnector connector = await CreateConnectorAsync(AuroraGrand, [
            new CmsSeed(AuroraGrand, ContentType.Faq, "Wi-Fi", "Join Aurora-Guest.", [], Published: true),
            new CmsSeed(AuroraGrand, ContentType.Faq, "Breakfast", "From 7am.", [], Published: true),
        ]);
        IReadOnlyList<CmsContent> first = await connector.ListPublishedContentAsync(Cancellation);
        CmsContent wifi = first.Single(item => item.Title == "Wi-Fi");
        CmsContent breakfast = first.Single(item => item.Title == "Breakfast");

        await RetitleAsync(wifi.Id, "Wi-Fi and streaming");
        IReadOnlyList<CmsContent> second = await connector.ListPublishedContentAsync(Cancellation);

        CmsContent changed = second.Single(item => item.Id == wifi.Id);
        Assert.Equal("Wi-Fi and streaming", changed.Title);
        Assert.NotEqual(wifi.Revision, changed.Revision);
        Assert.Equal(breakfast, second.Single(item => item.Id == breakfast.Id));
    }

    [Fact]
    public async Task ListPublishedContent_WhenNothingIsPublished_IsEmpty()
    {
        ICmsConnector connector = await CreateConnectorAsync(AuroraGrand, []);

        Assert.Empty(await connector.ListPublishedContentAsync(Cancellation));
    }
}

/// <summary>
/// A content item to arrange in a CMS.
/// </summary>
public sealed record CmsSeed(PropertyId PropertyId, ContentType Type, string Title, string Body, string[] Tags, bool Published);
