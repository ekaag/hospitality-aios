using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Api.Content;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using ContractStatus = HospitalityOS.Contracts.Api.Content.ContentStatus;
using ContractSubject = HospitalityOS.Contracts.Api.Properties.SubjectReference;
using ContractType = HospitalityOS.Contracts.Capabilities.Properties.ContentType;
using DomainType = HospitalityOS.Domain.Properties.ContentItems.ContentType;

namespace HospitalityOS.Application.Content;

internal sealed class ContentAdministration(
    IContentItemRepository contentItems,
    PropertyReferences references,
    ICurrentTenant currentTenant,
    AuditedCommit commit,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IContentAdministration
{
    public async Task<Result<ContentItemResponse>> CreateAsync(CreateContentItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PropertyId> property = await references.PropertyAsync(request.PropertyId, "content", cancellationToken).ConfigureAwait(false);

        if (property.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.ContentItemCreate, property.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();
        ContentItemDetails details = await DetailsAsync(property.Value, request.Type, request.Title, request.Body, request.About, request.Tags, fields, cancellationToken).ConfigureAwait(false);
        ContentItemId id = new(idGenerators.Resolve().NewId());
        Result<ContentItem> created = fields.Merge(ContentItem.Create(id, currentTenant.TenantId, property.Value, details, timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.ContentItemCreate,
            created,
            item => contentItems.AddAsync(item, cancellationToken),
            item => Target(item.Id.Value, item.PropertyId),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ContentItemResponse>> UpdateAsync(Guid id, UpdateContentItemRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await ChangeAsync(
            AdministrationActions.ContentItemUpdate,
            id,
            expectedVersion,
            async item =>
            {
                RequestFields fields = new();
                ContentItemDetails details = await DetailsAsync(item.PropertyId, request.Type, request.Title, request.Body, request.About, request.Tags, fields, cancellationToken).ConfigureAwait(false);

                return fields.Apply(() => item.Update(details, timeProvider.GetUtcNow()));
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<ContentItemResponse>> PublishAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(AdministrationActions.ContentItemPublish, id, expectedVersion, item => Task.FromResult(item.Publish(timeProvider.GetUtcNow())), cancellationToken);

    public Task<Result<ContentItemResponse>> ArchiveAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(AdministrationActions.ContentItemArchive, id, expectedVersion, item => Task.FromResult(item.Archive(timeProvider.GetUtcNow())), cancellationToken);

    public async Task<Result<ContentItemResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        ContentItem? item = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return item is null ? AdministrationErrors.ContentItemNotFound : Response(item);
    }

    public async Task<IReadOnlyList<ContentItemResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ContentItem> found = await contentItems.ListForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        return [.. found.OrderBy(item => item.Details.Title, StringComparer.OrdinalIgnoreCase).Select(Response)];
    }

    private async Task<Result<ContentItemResponse>> ChangeAsync(string action, Guid id, long? expectedVersion, Func<ContentItem, Task<Result>> change, CancellationToken cancellationToken)
    {
        ContentItem? item = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return await commit.ChangeAsync(
            action,
            Target(id, item?.PropertyId),
            item,
            AdministrationErrors.ContentItemNotFound,
            expectedVersion,
            change,
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    // An empty GUID cannot be a content item ID, so it names no item.
    private Task<ContentItem?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<ContentItem?>(null) : contentItems.GetByIdAsync(new ContentItemId(id), cancellationToken);

    private static AuditTarget Target(Guid id, PropertyId? propertyId) => new(nameof(ContentItem), id.ToString(), propertyId);

    // The domain validates every field, including missing text, so absent values pass through as null.
    private async Task<ContentItemDetails> DetailsAsync(
        PropertyId propertyId,
        ContractType? type,
        string? title,
        string? body,
        ContractSubject? about,
        ValueList<string>? tags,
        RequestFields fields,
        CancellationToken cancellationToken) =>
        new(
            fields.Required<DomainType>(type, "type", "content.type_required"),
            title!,
            body!,
            await references.SubjectAsync(propertyId, about, "about", "content.about", fields, cancellationToken).ConfigureAwait(false),
            tags);

    private static ContentItemResponse Response(ContentItem item)
    {
        ContentItemDetails details = item.Details;

        return new ContentItemResponse(
            item.Id.Value,
            item.PropertyId,
            EnumMirror.To<ContractType>(details.Type),
            details.Title,
            details.Body,
            Subjects.ToContractOrNull(details.About),
            details.Tags ?? [],
            EnumMirror.To<ContractStatus>(item.Status),
            item.Version);
    }
}
