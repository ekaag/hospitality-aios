using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Properties;

/// <summary>
/// Creates, updates, and reads the caller's tenant's properties (Journey 1). Commands commit their change and its audit
/// record together; rejected commands are audited too. Scoped; the caller's tenant and actor come from the operation.
/// </summary>
public interface IPropertyAdministration
{
    /// <summary>
    /// Creates a property for an organization of the tenant and raises <c>PropertyCreated</c>.
    /// </summary>
    /// <returns>The property, or a <see cref="ValidationError"/> listing every invalid field (an organization not in the
    /// tenant is the field <c>organizationId</c>).</returns>
    Task<Result<PropertyResponse>> CreateAsync(CreatePropertyRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a property's base information and raises <c>PropertyUpdated</c>; unchanged information is a no-op.
    /// </summary>
    /// <param name="id">The property.</param>
    /// <param name="request">The new information.</param>
    /// <param name="expectedVersion">The version the caller read, when it wants the change refused if someone changed the
    /// property since.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The property; <c>property.not_found</c>; <c>request.version_mismatch</c>; or a
    /// <see cref="ValidationError"/>.</returns>
    Task<Result<PropertyResponse>> UpdateAsync(PropertyId id, UpdatePropertyRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a property.
    /// </summary>
    /// <returns>The property, or <c>property.not_found</c>.</returns>
    Task<Result<PropertyResponse>> GetAsync(PropertyId id, CancellationToken cancellationToken);

    /// <summary>
    /// The tenant's properties, or one organization's, by name.
    /// </summary>
    Task<IReadOnlyList<PropertyResponse>> ListAsync(OrganizationId? organizationId, CancellationToken cancellationToken);
}
