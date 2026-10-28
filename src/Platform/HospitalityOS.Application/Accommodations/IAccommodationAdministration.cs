using HospitalityOS.Contracts.Api.Accommodations;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Accommodations;

/// <summary>
/// Creates and reads the room types and rooms of the caller's tenant's properties, so stays and service requests can name
/// a room. Commands commit their change and its audit record together; rejected commands are audited too. Scoped; the
/// caller's tenant and actor come from the operation. Phase 1 has no HTTP API for these: rooms come from the reference
/// data seed and, later, the PMS.
/// </summary>
public interface IAccommodationAdministration
{
    /// <summary>
    /// Creates a room type and raises <c>AccommodationTypeCreated</c>.
    /// </summary>
    /// <returns>The type, or a <see cref="ValidationError"/> listing every invalid field.</returns>
    Task<Result<AccommodationTypeResponse>> CreateTypeAsync(CreateAccommodationTypeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a room type.
    /// </summary>
    /// <returns>The type, or <c>accommodation_type.not_found</c>.</returns>
    Task<Result<AccommodationTypeResponse>> GetTypeAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a room and raises <c>AccommodationUnitCreated</c>.
    /// </summary>
    /// <returns>The room, or a <see cref="ValidationError"/> listing every invalid field (a type not in the tenant is
    /// <c>accommodationTypeId</c>; a space missing or in another property is <c>spaceId</c>; a number another room of the
    /// property has is <c>accommodation_unit.number_taken</c>).</returns>
    Task<Result<AccommodationUnitResponse>> CreateUnitAsync(CreateAccommodationUnitRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a room.
    /// </summary>
    /// <returns>The room, or <c>accommodation_unit.not_found</c>.</returns>
    Task<Result<AccommodationUnitResponse>> GetUnitAsync(AccommodationUnitId id, CancellationToken cancellationToken);
}
