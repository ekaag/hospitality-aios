using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Capabilities.Retrieval;

/// <summary>
/// Where published content is indexed: the <see cref="Name"/> index and vector collection, one document per content
/// item (its ID), scoped by property, embedded by the <see cref="EmbeddingTask"/> inference task. The search ports are
/// resolved per tenant, so a tenant can be bound to its own search service.
/// </summary>
internal static class ContentIndex
{
    public const string Name = "content";

    public const string EmbeddingTask = "content.embed";

    public static BindingContext Binding(TenantId tenantId) => new(Name, tenantId.Value.ToString());

    public static string Scope(ContentItem item) => item.PropertyId.Value.ToString();

    /// <summary>
    /// What is embedded for an item: its title, body, and tags.
    /// </summary>
    public static string Text(ContentItemDetails details) =>
        string.Join('\n', [details.Title, details.Body, .. details.Tags ?? []]);
}
