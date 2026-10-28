using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Ports.Search;

/// <summary>
/// A document in an <see cref="ISearchIndex"/>.
/// </summary>
/// <param name="Id">Its ID, unique per tenant in the index, e.g. the content item's ID.</param>
/// <param name="TenantId">The tenant it belongs to.</param>
/// <param name="Scope">The partition searches name, e.g. the property's ID.</param>
/// <param name="Kind">Its kind, for filtering, e.g. <c>Faq</c>.</param>
/// <param name="Title">Its title; words here count more than words in the body.</param>
/// <param name="Body">Its text.</param>
/// <param name="Tags">Keywords; they count like title words.</param>
public sealed record SearchDocument(string Id, TenantId TenantId, string Scope, string Kind, string Title, string Body, ValueList<string> Tags);

/// <summary>
/// A keyword search.
/// </summary>
/// <param name="TenantId">Only this tenant's documents.</param>
/// <param name="Scope">Only documents in this scope.</param>
/// <param name="Text">The words to look for, as a guest wrote them.</param>
/// <param name="Kind">Only documents of this kind, or any.</param>
/// <param name="Top">Most hits to return, at least 1.</param>
public sealed record KeywordQuery(TenantId TenantId, string Scope, string Text, string? Kind, int Top);

/// <summary>
/// A record in an <see cref="IVectorStore"/>.
/// </summary>
/// <param name="Id">Its ID, unique per tenant in the collection.</param>
/// <param name="TenantId">The tenant it belongs to.</param>
/// <param name="Scope">The partition searches name, e.g. the property's ID.</param>
/// <param name="Kind">Its kind, for filtering.</param>
/// <param name="Vector">Its embedding.</param>
public sealed record VectorRecord(string Id, TenantId TenantId, string Scope, string Kind, ReadOnlyMemory<float> Vector);

/// <summary>
/// A similarity search.
/// </summary>
/// <param name="TenantId">Only this tenant's records.</param>
/// <param name="Scope">Only records in this scope.</param>
/// <param name="Vector">The query's embedding, from the same model as the records'.</param>
/// <param name="Kind">Only records of this kind, or any.</param>
/// <param name="Top">Most hits to return, at least 1.</param>
/// <param name="MinScore">Least similarity a hit needs: unrelated records are not "nearest" answers.</param>
public sealed record VectorQuery(TenantId TenantId, string Scope, ReadOnlyMemory<float> Vector, string? Kind, int Top, double MinScore);

/// <summary>
/// A search result: a document or record ID and how well it matched (higher is better; scales differ per port).
/// </summary>
public sealed record SearchHit(string Id, double Score);
