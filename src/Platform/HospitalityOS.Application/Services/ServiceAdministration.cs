using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Api.Services;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using ContractCategory = HospitalityOS.Contracts.Api.Services.ServiceCategory;
using DomainCategory = HospitalityOS.Domain.Services.Services.ServiceCategory;

namespace HospitalityOS.Application.Services;

internal sealed class ServiceAdministration(
    IServiceRepository services,
    PropertyReferences references,
    ICurrentTenant currentTenant,
    AuditedCommit commit,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IServiceAdministration
{
    public async Task<Result<ServiceResponse>> CreateAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PropertyId> property = await references.PropertyAsync(request.PropertyId, "service", cancellationToken).ConfigureAwait(false);

        if (property.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.ServiceCreate, property.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();
        await CheckCodeAsync(property.Value, request.Code, null, fields, cancellationToken).ConfigureAwait(false);
        ServiceDetails details = Details(request.Code, request.Name, request.Category, request.Description, request.IsGuestRequestable, request.Items, request.TargetCompletion, fields);
        ServiceId id = new(idGenerators.Resolve().NewId());
        Result<Service> created = fields.Merge(Service.Create(id, currentTenant.TenantId, property.Value, details, timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.ServiceCreate,
            created,
            service => services.AddAsync(service, cancellationToken),
            service => Target(service.Id.Value, service.PropertyId),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ServiceResponse>> UpdateAsync(Guid id, UpdateServiceRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Service? service = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return await commit.ChangeAsync(
            AdministrationActions.ServiceUpdate,
            Target(id, service?.PropertyId),
            service,
            AdministrationErrors.ServiceNotFound,
            expectedVersion,
            async found =>
            {
                RequestFields fields = new();
                await CheckCodeAsync(found.PropertyId, request.Code, found.Id, fields, cancellationToken).ConfigureAwait(false);
                ServiceDetails details = Details(request.Code, request.Name, request.Category, request.Description, request.IsGuestRequestable, request.Items, request.TargetCompletion, fields);

                return fields.Apply(() => found.Update(details, timeProvider.GetUtcNow()));
            },
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ServiceResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Service? service = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return service is null ? AdministrationErrors.ServiceNotFound : Response(service);
    }

    public async Task<IReadOnlyList<ServiceResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Service> found = await services.ListForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        return [.. found.OrderBy(service => service.Details.Name, StringComparer.OrdinalIgnoreCase).Select(Response)];
    }

    // Codes are unique within a property (guest requests and extraction name services by code). The domain cannot see
    // other services, so the check is here; a unique index catches two administrators racing for the same code.
    private async Task CheckCodeAsync(PropertyId propertyId, string? code, ServiceId? self, RequestFields fields, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        Service? holder = await services.FindByCodeAsync(propertyId, code, cancellationToken).ConfigureAwait(false);

        if (holder is not null && holder.Id != self)
        {
            fields.Add("code", "service.code_taken", $"Another service of this property has the code '{holder.Details.Code}'.");
        }
    }

    // An empty GUID cannot be a service ID, so it names no service.
    private Task<Service?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<Service?>(null) : services.GetByIdAsync(new ServiceId(id), cancellationToken);

    private static AuditTarget Target(Guid id, PropertyId? propertyId) => new(nameof(Service), id.ToString(), propertyId);

    // The domain validates every field, including missing text, so absent values pass through as null; a missing item
    // becomes a null entry, which the domain reports by position.
    private static ServiceDetails Details(
        string? code,
        string? name,
        ContractCategory? category,
        string? description,
        bool? isGuestRequestable,
        ValueList<ServiceItemRequest>? items,
        TimeSpan? targetCompletion,
        RequestFields fields) =>
        new(
            code!,
            name!,
            fields.Required<DomainCategory>(category, "category", "service.category_required"),
            description,
            isGuestRequestable ?? true,
            ValueList.From((items ?? []).Select((item, index) => item is null ? null! : Item(item, $"items[{index}]", fields))),
            targetCompletion);

    private static ServiceItem Item(ServiceItemRequest item, string path, RequestFields fields)
    {
        if (item.MaxQuantityPerRequest is null)
        {
            fields.Add(path + ".maxQuantityPerRequest", "service.item_max_quantity_required", $"{path}.maxQuantityPerRequest is required.");
        }

        // A placeholder the domain accepts when the limit is missing: the command is rejected anyway.
        return new ServiceItem(item.Code!, item.Name!, item.MaxQuantityPerRequest ?? 1);
    }

    private static ServiceResponse Response(Service service)
    {
        ServiceDetails details = service.Details;

        return new ServiceResponse(
            service.Id.Value,
            service.PropertyId,
            details.Code,
            details.Name,
            EnumMirror.To<ContractCategory>(details.Category),
            details.Description,
            details.IsGuestRequestable,
            ValueList.From(details.Items!.Select(item => new ServiceItemResponse(item.Code, item.Name, item.MaxQuantityPerRequest))),
            details.TargetCompletion,
            service.Version);
    }
}
