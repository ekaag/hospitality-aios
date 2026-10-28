using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Api.Amenities;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using ContractCategory = HospitalityOS.Contracts.Capabilities.Properties.AmenityCategory;
using DomainCategory = HospitalityOS.Domain.Properties.Amenities.AmenityCategory;

namespace HospitalityOS.Application.Amenities;

internal sealed class AmenityAdministration(
    IAmenityRepository amenities,
    PropertyReferences references,
    ICurrentTenant currentTenant,
    AuditedCommit commit,
    RecordIds ids,
    TimeProvider timeProvider) : IAmenityAdministration
{
    private const string LocationField = "locationSpaceId";
    private const string LocationNotFound = "amenity.location_not_found";

    public async Task<Result<AmenityResponse>> CreateAsync(CreateAmenityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PropertyId> property = await references.PropertyAsync(request.PropertyId, "amenity", cancellationToken).ConfigureAwait(false);

        if (property.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.AmenityCreate, property.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();
        Space? location = await references.SpaceAsync(request.LocationSpaceId, LocationField, LocationNotFound, fields, cancellationToken).ConfigureAwait(false);
        AmenityDetails details = Details(request.Name, request.Category, request.Description, fields);
        AmenityId id = new(ids.Next());
        Result<Amenity> created = fields.Merge(Amenity.Create(id, currentTenant.TenantId, property.Value, details, location, timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.AmenityCreate,
            created,
            amenity => amenities.AddAsync(amenity, cancellationToken),
            amenity => Target(amenity.Id, amenity.PropertyId),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AmenityResponse>> UpdateAsync(Guid id, UpdateAmenityRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Amenity? amenity = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return await commit.ChangeAsync(
            AdministrationActions.AmenityUpdate,
            Target(id, amenity?.PropertyId),
            amenity,
            AdministrationErrors.AmenityNotFound,
            expectedVersion,
            async found =>
            {
                RequestFields fields = new();
                Space? location = await references.SpaceAsync(request.LocationSpaceId, LocationField, LocationNotFound, fields, cancellationToken).ConfigureAwait(false);
                AmenityDetails details = Details(request.Name, request.Category, request.Description, fields);

                return fields.Apply(() => found.Update(details, location, timeProvider.GetUtcNow()));
            },
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AmenityResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Amenity? amenity = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return amenity is null ? AdministrationErrors.AmenityNotFound : Response(amenity);
    }

    public async Task<IReadOnlyList<AmenityResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Amenity> found = await amenities.ListForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        return [.. found.OrderBy(amenity => amenity.Details.Name, StringComparer.OrdinalIgnoreCase).Select(Response)];
    }

    // An empty GUID cannot be an amenity ID, so it names no amenity.
    private Task<Amenity?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<Amenity?>(null) : amenities.GetByIdAsync(new AmenityId(id), cancellationToken);

    private static AuditTarget Target(AmenityId id, PropertyId? propertyId) => Target(id.Value, propertyId);

    private static AuditTarget Target(Guid id, PropertyId? propertyId) => new(nameof(Amenity), id.ToString(), propertyId);

    // The domain validates every field, including missing text, so absent values pass through as null.
    private static AmenityDetails Details(string? name, ContractCategory? category, string? description, RequestFields fields) =>
        new(name!, fields.Required<DomainCategory>(category, "category", "amenity.category_required"), description);

    private static AmenityResponse Response(Amenity amenity) =>
        new(
            amenity.Id.Value,
            amenity.PropertyId,
            amenity.Details.Name,
            EnumMirror.To<ContractCategory>(amenity.Details.Category),
            amenity.Details.Description,
            amenity.LocationSpaceId,
            amenity.Version);
}
