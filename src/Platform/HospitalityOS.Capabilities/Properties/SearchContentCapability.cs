using System.Text.RegularExpressions;
using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Capabilities.Retrieval;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using ContractContentType = HospitalityOS.Contracts.Capabilities.Properties.ContentType;
using ContractSubjectKind = HospitalityOS.Contracts.Capabilities.SubjectKind;
using DomainContentType = HospitalityOS.Domain.Properties.ContentItems.ContentType;

namespace HospitalityOS.Capabilities.Properties;

/// <summary>
/// <c>Property.SearchContent</c>: hybrid keyword and semantic search over the property's published content (FAQs,
/// directions, notices), through the search index and vector store (<see cref="ContentSearch"/>). The query must hold at
/// least one meaningful word.
/// </summary>
internal sealed partial class SearchContentCapability(
    IPropertyRepository properties,
    ContentSearch search) : ICapability<SearchContentInput, SearchContentOutput>
{
    public const int MaxLimit = 20;

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

        if (!HasMeaningfulWord(input.Query))
        {
            return ReadErrors.Required("query");
        }

        if (await properties.GetByIdAsync(input.PropertyId, cancellationToken).ConfigureAwait(false) is null)
        {
            return ReadErrors.PropertyNotFound;
        }

        DomainContentType? type = input.Type is { } contractType ? EnumMirror.To<DomainContentType>(contractType) : null;
        IReadOnlyList<ContentItem> found = await search.SearchAsync(input.PropertyId, input.Query, type, input.Limit, cancellationToken).ConfigureAwait(false);

        return new SearchContentOutput(ValueList.From(found.Select(item => new ContentEntry(
            Sources.Of(item),
            EnumMirror.To<ContractContentType>(item.Details.Type),
            item.Details.Title,
            item.Details.Body,
            item.Details.Tags ?? [],
            item.Details.About is { } about ? new Subject(EnumMirror.To<ContractSubjectKind>(about.Kind), about.Id) : null))));
    }

    private static bool HasMeaningfulWord(string? query) =>
        query is not null && Word().Matches(query.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal))
            .Any(match => match.Value.Length > 1 && !ignoredWords.Contains(match.Value));

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();
}
