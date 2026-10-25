using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Domain.Properties.ContentItems.Events;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Properties.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Properties.ContentItems;

public sealed class ContentItemTests
{
    private static readonly ContentItemId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209907001"));
    private static readonly SubjectReference poolDeck = new(SubjectKind.Space, Guid.Parse("01890a5d-ac96-774b-bcce-b30209907002"));

    private static readonly ContentItemDetails poolFaq = new(
        ContentType.Faq,
        "Is the pool heated?",
        "Yes, the pool is heated to 28°C year-round.",
        poolDeck,
        ["pool", "swimming"]);

    [Fact]
    public void Create_StartsAsDraftAndRaisesCreated()
    {
        ContentItem item = ContentItem.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, poolFaq, AuroraGrand.Now).Value;

        Assert.Equal(ContentStatus.Draft, item.Status);
        Assert.Equal([new ContentItemCreated(id, AuroraGrand.PropertyId, poolFaq, ContentStatus.Draft, AuroraGrand.Now)], item.DomainEvents);
    }

    [Fact]
    public void Create_NormalizesTags()
    {
        ContentItem item = ContentItem.Create(
            id, AuroraGrand.TenantId, AuroraGrand.PropertyId, poolFaq with { Tags = [" Swimming ", "POOL", "pool"] }, AuroraGrand.Now).Value;

        Assert.Equal(["pool", "swimming"], item.Details.Tags!);
    }

    [Fact]
    public void Create_WithoutTags_StoresEmptyTags()
    {
        ContentItem item = ContentItem.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, poolFaq with { Tags = null }, AuroraGrand.Now).Value;

        Assert.Equal(ValueList.From(Array.Empty<string>()), item.Details.Tags);
    }

    [Fact]
    public void Create_WithInvalidFields_ReportsAll()
    {
        ValueList<string> tooManyTags = ValueList.From(Enumerable.Range(0, ContentItem.MaxTags + 1).Select(index => $"tag{index}"));
        ContentItemDetails invalid = new((ContentType)99, " ", "", new SubjectReference((SubjectKind)99, poolDeck.Id), tooManyTags);

        Result<ContentItem> result = ContentItem.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, invalid, AuroraGrand.Now);

        Assert.Equal(
            [
                ("title", "content.title_required"),
                ("body", "content.body_required"),
                ("tags", "content.too_many_tags"),
                ("type", "content.type_invalid"),
                ("about.kind", "content.about_kind_invalid"),
            ],
            FieldCodes(result));
    }

    [Fact]
    public void Publish_ThenArchive_ThenPublishAgain_RaisesUpdatedEachTime()
    {
        ContentItem item = CreateDraft();

        item.Publish(AuroraGrand.Now);
        item.Archive(AuroraGrand.Now.AddDays(1));
        item.Publish(AuroraGrand.Now.AddDays(2));

        Assert.Equal(ContentStatus.Published, item.Status);
        Assert.Equal(
            [ContentStatus.Published, ContentStatus.Archived, ContentStatus.Published],
            item.DomainEvents.Cast<ContentItemUpdated>().Select(updated => updated.Status));
    }

    [Fact]
    public void Publish_WhenPublished_IsNoOp()
    {
        ContentItem item = CreateDraft();
        item.Publish(AuroraGrand.Now);
        item.DequeueDomainEvents();

        item.Publish(AuroraGrand.Now.AddHours(1));

        Assert.Empty(item.DomainEvents);
    }

    [Fact]
    public void Update_WithSameTextAndReorderedTags_IsNoOp()
    {
        ContentItem item = CreateDraft();

        Result result = item.Update(poolFaq with { Tags = ["swimming", "pool"] }, AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(item.DomainEvents);
    }

    private static ContentItem CreateDraft()
    {
        ContentItem item = ContentItem.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, poolFaq, AuroraGrand.Now).Value;
        item.DequeueDomainEvents();

        return item;
    }

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
