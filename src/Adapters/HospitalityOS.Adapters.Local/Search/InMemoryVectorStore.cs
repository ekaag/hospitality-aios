using System.Collections.Concurrent;
using HospitalityOS.Ports.Search;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Adapters.Local.Search;

/// <summary>
/// A vector store in process memory that compares every record by cosine similarity, for development, tests, and
/// evaluation runs (one property's content is small); nothing survives a restart.
/// </summary>
internal sealed class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<(string Collection, TenantId TenantId, string Id), VectorRecord> records = new();

    public Task UpsertAsync(string collection, VectorRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        records[(collection, record.TenantId, record.Id)] = record with { Vector = record.Vector.ToArray() };

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string collection, TenantId tenantId, string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        records.TryRemove((collection, tenantId, id), out _);

        return Task.CompletedTask;
    }

    public Task DeleteScopeAsync(string collection, TenantId tenantId, string scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var key in records.Where(entry => entry.Key.Collection == collection && entry.Key.TenantId == tenantId && entry.Value.Scope == scope).Select(entry => entry.Key))
        {
            records.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string collection, VectorQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Top, 1);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<SearchHit> hits = [.. records
            .Where(entry => entry.Key.Collection == collection && entry.Key.TenantId == query.TenantId)
            .Select(entry => entry.Value)
            .Where(record => record.Scope == query.Scope && (query.Kind is null || record.Kind == query.Kind) && record.Vector.Length == query.Vector.Length)
            .Select(record => new SearchHit(record.Id, Similarity(record.Vector.Span, query.Vector.Span)))
            .Where(hit => hit.Score >= query.MinScore)
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Id, StringComparer.Ordinal)
            .Take(query.Top)];

        return Task.FromResult(hits);
    }

    // Zero vectors (e.g. text with no words) are similar to nothing.
    private static double Similarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        double dot = 0;
        double leftLength = 0;
        double rightLength = 0;

        for (int i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftLength += left[i] * left[i];
            rightLength += right[i] * right[i];
        }

        return leftLength == 0 || rightLength == 0 ? 0 : dot / (Math.Sqrt(leftLength) * Math.Sqrt(rightLength));
    }
}
