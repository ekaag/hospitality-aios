using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts.Api.Accommodations;
using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Accommodations;

internal sealed class AccommodationAdministration(
    IAccommodationTypeRepository types,
    IAccommodationUnitRepository units,
    PropertyReferences references,
    ICurrentTenant currentTenant,
    AuditedCommit commit,
    RecordIds ids,
    TimeProvider timeProvider) : IAccommodationAdministration
{
    public async Task<Result<AccommodationTypeResponse>> CreateTypeAsync(CreateAccommodationTypeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PropertyId> property = await references.PropertyAsync(request.PropertyId, "accommodation_type", cancellationToken).ConfigureAwait(false);

        if (property.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.AccommodationTypeCreate, property.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();

        if (request.MaxOccupancy is null)
        {
            fields.Add("maxOccupancy", "accommodation_type.max_occupancy_required", "maxOccupancy is required.");
        }

        // The domain validates every field, including missing text; a missing occupancy gets a placeholder it accepts.
        AccommodationTypeDetails details = new(request.Name!, request.MaxOccupancy ?? 1, request.Description);
        AccommodationTypeId id = new(ids.Next());
        Result<AccommodationType> created = fields.Merge(AccommodationType.Create(id, currentTenant.TenantId, property.Value, details, timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.AccommodationTypeCreate,
            created,
            type => types.AddAsync(type, cancellationToken),
            type => new AuditTarget(nameof(AccommodationType), type.Id.Value.ToString(), type.PropertyId),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AccommodationTypeResponse>> GetTypeAsync(Guid id, CancellationToken cancellationToken)
    {
        AccommodationType? type = await FindTypeAsync(id, cancellationToken).ConfigureAwait(false);

        return type is null ? AdministrationErrors.AccommodationTypeNotFound : Response(type);
    }

    public async Task<Result<AccommodationUnitResponse>> CreateUnitAsync(CreateAccommodationUnitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<AccommodationType> type = await TypeAsync(request.AccommodationTypeId, cancellationToken).ConfigureAwait(false);

        if (type.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.AccommodationUnitCreate, type.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();
        Space? space = await SpaceAsync(request.SpaceId, fields, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(request.UnitNumber)
            && await units.FindByUnitNumberAsync(type.Value.PropertyId, request.UnitNumber, cancellationToken).ConfigureAwait(false) is { } holder)
        {
            fields.Add("unitNumber", "accommodation_unit.number_taken", $"Another room of this property is numbered '{holder.UnitNumber}'.");
        }

        AccommodationUnitId id = new(ids.Next());

        // Without a space the domain cannot run; the request's own errors are then the whole answer.
        Result<AccommodationUnit> created = space is null
            ? fields.ToError()
            : fields.Merge(AccommodationUnit.Create(id, type.Value, space, request.UnitNumber!, timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.AccommodationUnitCreate,
            created,
            unit => units.AddAsync(unit, cancellationToken),
            unit => new AuditTarget(nameof(AccommodationUnit), unit.Id.ToString(), unit.PropertyId),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AccommodationUnitResponse>> GetUnitAsync(AccommodationUnitId id, CancellationToken cancellationToken)
    {
        AccommodationUnit? unit = await units.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return unit is null ? AdministrationErrors.AccommodationUnitNotFound : Response(unit);
    }

    /// <summary>
    /// The type a new room is of, which must exist in the caller's tenant; the room takes its property.
    /// </summary>
    private async Task<Result<AccommodationType>> TypeAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id is not { } typeId)
        {
            return Error.Validation("accommodationTypeId", "accommodation_unit.type_required", "accommodationTypeId is required.");
        }

        AccommodationType? type = await FindTypeAsync(typeId, cancellationToken).ConfigureAwait(false);

        return type is null ? Error.Validation("accommodationTypeId", "accommodation_unit.type_not_found", "There is no such room type.") : type;
    }

    private async Task<Space?> SpaceAsync(SpaceId? id, RequestFields fields, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            fields.Add("spaceId", "accommodation_unit.space_required", "spaceId is required.");

            return null;
        }

        // A space of another property is returned, for the domain to refuse with its own error.
        return await references.SpaceAsync(id, "spaceId", "accommodation_unit.space_not_found", fields, cancellationToken).ConfigureAwait(false);
    }

    // An empty GUID cannot be a type ID, so it names no type.
    private Task<AccommodationType?> FindTypeAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<AccommodationType?>(null) : types.GetByIdAsync(new AccommodationTypeId(id), cancellationToken);

    private static AccommodationTypeResponse Response(AccommodationType type) =>
        new(type.Id.Value, type.PropertyId, type.Details.Name, type.Details.MaxOccupancy, type.Details.Description, type.Version);

    private static AccommodationUnitResponse Response(AccommodationUnit unit) =>
        new(unit.Id, unit.PropertyId, unit.AccommodationTypeId.Value, unit.SpaceId, unit.UnitNumber, unit.Version);
}
