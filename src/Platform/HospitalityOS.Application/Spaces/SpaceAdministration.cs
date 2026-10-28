using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Api.Spaces;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using ContractKind = HospitalityOS.Contracts.Api.Spaces.SpaceKind;
using DomainKind = HospitalityOS.Domain.Properties.Spaces.SpaceKind;

namespace HospitalityOS.Application.Spaces;

internal sealed class SpaceAdministration(
    ISpaceRepository spaces,
    PropertyReferences references,
    ICurrentTenant currentTenant,
    AuditedCommit commit,
    RecordIds ids,
    TimeProvider timeProvider) : ISpaceAdministration
{
    public async Task<Result<SpaceResponse>> CreateAsync(CreateSpaceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PropertyId> property = await references.PropertyAsync(request.PropertyId, "space", cancellationToken).ConfigureAwait(false);

        if (property.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.SpaceCreate, property.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();
        Space? parent = await references.SpaceAsync(request.ParentSpaceId, "parentSpaceId", "space.parent_not_found", fields, cancellationToken).ConfigureAwait(false);
        SpaceDetails details = Details(request.Name, request.Kind, request.Code, request.Description, request.Directions, fields);
        SpaceId id = new(ids.Next());
        Result<Space> created = fields.Merge(Space.Create(id, currentTenant.TenantId, property.Value, parent, details, timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.SpaceCreate,
            created,
            space => spaces.AddAsync(space, cancellationToken),
            Target,
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<SpaceResponse>> UpdateAsync(SpaceId id, UpdateSpaceRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Space? space = await spaces.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return await commit.ChangeAsync(
            AdministrationActions.SpaceUpdate,
            new AuditTarget(nameof(Space), id.ToString(), space?.PropertyId),
            space,
            AdministrationErrors.SpaceNotFound,
            expectedVersion,
            found =>
            {
                RequestFields fields = new();
                SpaceDetails details = Details(request.Name, request.Kind, request.Code, request.Description, request.Directions, fields);

                return Task.FromResult(fields.Apply(() => found.Update(details, timeProvider.GetUtcNow())));
            },
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<SpaceResponse>> GetAsync(SpaceId id, CancellationToken cancellationToken)
    {
        Space? space = await spaces.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return space is null ? AdministrationErrors.SpaceNotFound : Response(space);
    }

    public async Task<IReadOnlyList<SpaceResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Space> found = await spaces.ListForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        return [.. found.OrderBy(space => space.Details.Name, StringComparer.OrdinalIgnoreCase).Select(Response)];
    }

    private static AuditTarget Target(Space space) => new(nameof(Space), space.Id.ToString(), space.PropertyId);

    // The domain validates every field, including missing text, so absent values pass through as null.
    private static SpaceDetails Details(string? name, ContractKind? kind, string? code, string? description, string? directions, RequestFields fields) =>
        new(name!, fields.Required<DomainKind>(kind, "kind", "space.kind_required"), code, description, directions);

    private static SpaceResponse Response(Space space)
    {
        SpaceDetails details = space.Details;

        return new SpaceResponse(
            space.Id,
            space.PropertyId,
            space.ParentSpaceId,
            details.Name,
            EnumMirror.To<ContractKind>(details.Kind),
            details.Code,
            details.Description,
            details.Directions,
            space.Version);
    }
}
