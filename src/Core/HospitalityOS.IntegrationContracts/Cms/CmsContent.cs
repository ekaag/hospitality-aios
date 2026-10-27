using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.IntegrationContracts.Cms;

/// <summary>
/// A published content item as the CMS reports it, in canonical shape; the runtime turns it into a <c>ContentItem</c>.
/// </summary>
/// <param name="Id">The provider's ID for the item.</param>
/// <param name="Type">The canonical kind of content; the connector maps the provider's own categories to it.</param>
/// <param name="Title">The item's title.</param>
/// <param name="Body">The item's text.</param>
/// <param name="Tags">Search keywords.</param>
/// <param name="UpdatedAt">When the provider last changed the item, so unchanged items can be skipped.</param>
public sealed record CmsContent(
    ExternalId Id,
    ContentType Type,
    string Title,
    string Body,
    ValueList<string> Tags,
    DateTimeOffset UpdatedAt);
