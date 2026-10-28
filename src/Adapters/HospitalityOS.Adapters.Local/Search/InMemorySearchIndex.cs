using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HospitalityOS.Ports.Search;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.Local.Search;

/// <summary>
/// A keyword index in process memory, for development, tests, and evaluation runs: nothing survives a restart, so
/// indexes are rebuilt from their store. Each query word found in a document's title or tags scores 3, in its body 1;
/// matching ignores case and hyphens ("wifi" finds "Wi-Fi") and common words.
/// </summary>
internal sealed partial class InMemorySearchIndex : ISearchIndex
{
    private const int TitleOrTagWeight = 3;
    private const int BodyWeight = 1;

    private static readonly HashSet<string> ignoredWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "can", "do", "does", "for", "how", "i", "in", "is", "it", "my", "of", "on", "or", "the", "to", "what", "when", "where", "you", "your",
    };

    private readonly ConcurrentDictionary<(string Index, TenantId TenantId, string Id), Indexed> documents = new();

    public Task UpsertAsync(string index, SearchDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        documents[(index, document.TenantId, document.Id)] = new Indexed(
            document,
            Words(document.Title).Concat(document.Tags.SelectMany(Words)).ToHashSet(StringComparer.Ordinal),
            Words(document.Body).ToHashSet(StringComparer.Ordinal));

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string index, TenantId tenantId, string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        documents.TryRemove((index, tenantId, id), out _);

        return Task.CompletedTask;
    }

    public Task DeleteScopeAsync(string index, TenantId tenantId, string scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var key in documents.Where(entry => entry.Key.Index == index && entry.Key.TenantId == tenantId && entry.Value.Document.Scope == scope).Select(entry => entry.Key))
        {
            documents.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string index, KeywordQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Top, 1);
        cancellationToken.ThrowIfCancellationRequested();

        string[] terms = [.. Words(query.Text).Distinct(StringComparer.Ordinal)];
        IReadOnlyList<SearchHit> hits = [.. documents
            .Where(entry => entry.Key.Index == index && entry.Key.TenantId == query.TenantId)
            .Select(entry => entry.Value)
            .Where(indexed => indexed.Document.Scope == query.Scope && (query.Kind is null || indexed.Document.Kind == query.Kind))
            .Select(indexed => new SearchHit(indexed.Document.Id, terms.Sum(term => (indexed.Heading.Contains(term) ? TitleOrTagWeight : 0) + (indexed.Body.Contains(term) ? BodyWeight : 0))))
            .Where(hit => hit.Score > 0)
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Id, StringComparer.Ordinal)
            .Take(query.Top)];

        return Task.FromResult(hits);
    }

    private static IEnumerable<string> Words(string text) =>
        Word().Matches(text.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal))
            .Select(match => match.Value)
            .Where(word => word.Length > 1 && !ignoredWords.Contains(word));

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();

    private sealed record Indexed(SearchDocument Document, HashSet<string> Heading, HashSet<string> Body);
}
