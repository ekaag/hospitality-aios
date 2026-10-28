using HospitalityOS.Contracts.Api.Spaces;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Spaces;

/// <summary>
/// Creates, updates, and reads the places of the caller's tenant's properties. Commands commit their change and its
/// audit record together; rejected commands are audited too. Scoped; the caller's tenant and actor come from the
/// operation.
/// </summary>
public interface ISpaceAdministration
{
    /// <summary>
    /// Creates a space in a property, optionally inside another of its spaces, and raises <c>SpaceCreated</c>.
    /// </summary>
    /// <returns>The space, or a <see cref="ValidationError"/> listing every invalid field (a property not in the tenant
    /// is the field <c>propertyId</c>; a parent not in the property is <c>parentSpaceId</c>).</returns>
    Task<Result<SpaceResponse>> CreateAsync(CreateSpaceRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a space's descriptive information and raises <c>SpaceUpdated</c>; unchanged information is a no-op.
    /// </summary>
    /// <param name="id">The space.</param>
    /// <param name="request">The new information.</param>
    /// <param name="expectedVersion">The version the caller read, when it wants the change refused if someone changed the
    /// space since.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The space; <c>space.not_found</c>; <c>request.version_mismatch</c>; or a <see cref="ValidationError"/>.</returns>
    Task<Result<SpaceResponse>> UpdateAsync(SpaceId id, UpdateSpaceRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a space.
    /// </summary>
    /// <returns>The space, or <c>space.not_found</c>.</returns>
    Task<Result<SpaceResponse>> GetAsync(SpaceId id, CancellationToken cancellationToken);

    /// <summary>
    /// A property's spaces, by name.
    /// </summary>
    Task<IReadOnlyList<SpaceResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
