using HospitalityOS.Contracts.Api.Content;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Content;

/// <summary>
/// Drafts, edits, publishes, archives, and reads the descriptive content of the caller's tenant's properties. Commands
/// commit their change and its audit record together; rejected commands are audited too. Scoped; the caller's tenant
/// and actor come from the operation.
/// </summary>
public interface IContentAdministration
{
    /// <summary>
    /// Drafts a content item for a property and raises <c>ContentItemCreated</c>.
    /// </summary>
    /// <returns>The draft, or a <see cref="ValidationError"/> listing every invalid field (a subject not in the property
    /// is <c>about.id</c>).</returns>
    Task<Result<ContentItemResponse>> CreateAsync(CreateContentItemRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a content item's text and raises <c>ContentItemUpdated</c>; unchanged text is a no-op.
    /// </summary>
    /// <param name="id">The content item.</param>
    /// <param name="request">The new text.</param>
    /// <param name="expectedVersion">The version the caller read, when it wants the change refused if someone changed the
    /// item since.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The item; <c>content.not_found</c>; <c>request.version_mismatch</c>; or a <see cref="ValidationError"/>.</returns>
    Task<Result<ContentItemResponse>> UpdateAsync(Guid id, UpdateContentItemRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Makes a content item visible to guests and the concierge and raises <c>ContentItemUpdated</c>; publishing
    /// published content is a no-op.
    /// </summary>
    /// <returns>The item; <c>content.not_found</c>; or <c>request.version_mismatch</c>.</returns>
    Task<Result<ContentItemResponse>> PublishAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Withdraws a content item and raises <c>ContentItemUpdated</c>; it can be published again.
    /// </summary>
    /// <returns>The item; <c>content.not_found</c>; or <c>request.version_mismatch</c>.</returns>
    Task<Result<ContentItemResponse>> ArchiveAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a content item.
    /// </summary>
    /// <returns>The item, or <c>content.not_found</c>.</returns>
    Task<Result<ContentItemResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// A property's content in every status, by title.
    /// </summary>
    Task<IReadOnlyList<ContentItemResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
