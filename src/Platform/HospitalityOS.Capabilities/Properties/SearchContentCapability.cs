using System.Text.RegularExpressions;
using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using ContractContentType = HospitalityOS.Contracts.Capabilities.Properties.ContentType;
using ContractSubjectKind = HospitalityOS.Contracts.Capabilities.SubjectKind;

namespace HospitalityOS.Capabilities.Properties;

/// <summary>
/// <c>Property.SearchContent</c>: keyword search over the property's published content (FAQs, directions, notices).
/// A word in the title or a tag counts more than one in the body. Phase 1 scale (one property's content) is searched
/// in memory; <c>ISearchIndex</c> and semantic retrieval replace this in G5 behind the same contract.
/// </summary>
internal sealed partial class SearchContentCapability(
    IPropertyRepository properties,
    IContentItemRepository content) : ICapability<SearchContentInput, SearchContentOutput>
{
    public const int MaxLimit = 20;

    private const int TitleOrTagWeight = 3;
    private const int BodyWeight = 1;

    private static readonly HashSet<string> ignoredWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "can", "do", "does", "for", "how", "i", "in", "is", "it", "my", "of", "on", "or", "the", "to", "what", "when", "where", "you", "your",
    };

    public static CapabilityDescriptor Descriptor { get; } = ReadCapability.Describe(
        "Property.SearchContent",
        "Searches the property's published information (FAQs, directions, announcements, overviews) for the given words and "
            + "returns the best matches. Use it for questions the other capabilities do not cover, e.g. Wi-Fi, parking, or "
            + "directions. Answer only from the returned text; if nothing matches, say you do not know.");

    public async Task<Result<SearchContentOutput>> ExecuteAsync(SearchContentInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Limit is < 1 or > MaxLimit)
        {
            return ReadErrors.OutOfRange("limit", 1, MaxLimit);
        }

        string[] terms = Terms(input.Query);

        if (terms.Length == 0)
        {
            return ReadErrors.Required("query");
        }

        if (await properties.GetByIdAsync(input.PropertyId, cancellationToken).ConfigureAwait(false) is null)
        {
            return ReadErrors.PropertyNotFound;
        }

        IReadOnlyList<ContentItem> published = await content.ListPublishedForPropertyAsync(input.PropertyId, cancellationToken).ConfigureAwait(false);

        IEnumerable<ContentItem> ranked = published
            .Where(item => input.Type is null || ContractEnum.To<ContractContentType>(item.Details.Type) == input.Type)
            .Select(item => (Item: item, Score: Score(item.Details, terms)))
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Item.Details.Title, StringComparer.OrdinalIgnoreCase)
            .Take(input.Limit)
            .Select(match => match.Item);

        return new SearchContentOutput(ValueList.From(ranked.Select(item => new ContentEntry(
            Sources.Of(item),
            ContractEnum.To<ContractContentType>(item.Details.Type),
            item.Details.Title,
            item.Details.Body,
            item.Details.Tags ?? [],
            item.Details.About is { } about ? new Subject(ContractEnum.To<ContractSubjectKind>(about.Kind), about.Id) : null))));
    }

    private static string[] Terms(string? query) =>
        query is null
            ? []
            : [.. Word().Matches(Normalize(query))
                .Select(match => match.Value)
                .Where(word => word.Length > 1 && !ignoredWords.Contains(word))
                .Distinct(StringComparer.Ordinal)];

    private static int Score(ContentItemDetails details, string[] terms)
    {
        HashSet<string> title = [.. Word().Matches(Normalize(details.Title)).Select(match => match.Value)];
        HashSet<string> body = [.. Word().Matches(Normalize(details.Body)).Select(match => match.Value)];
        HashSet<string> tags = new((details.Tags ?? []).SelectMany(tag => Word().Matches(Normalize(tag)).Select(match => match.Value)), StringComparer.Ordinal);

        return terms.Sum(term => (title.Contains(term) || tags.Contains(term) ? TitleOrTagWeight : 0) + (body.Contains(term) ? BodyWeight : 0));
    }

    /// <summary>
    /// Lower case without hyphens, so "wifi" finds "Wi-Fi".
    /// </summary>
    private static string Normalize(string text) => text.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();
}
