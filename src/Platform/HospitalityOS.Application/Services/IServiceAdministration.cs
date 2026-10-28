using HospitalityOS.Contracts.Api.Services;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Services;

/// <summary>
/// Defines, updates, and reads the services of the caller's tenant's properties: what guests can request and which team
/// fulfils it. Commands commit their change and its audit record together; rejected commands are audited too. Scoped;
/// the caller's tenant and actor come from the operation.
/// </summary>
public interface IServiceAdministration
{
    /// <summary>
    /// Defines a service at a property and raises <c>ServiceCreated</c>.
    /// </summary>
    /// <returns>The service, or a <see cref="ValidationError"/> listing every invalid field (a property not in the tenant
    /// is the field <c>propertyId</c>; a code another service of the property has is <c>service.code_taken</c>; item
    /// problems are reported by position, e.g. <c>items[1].code</c>).</returns>
    Task<Result<ServiceResponse>> CreateAsync(CreateServiceRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a service's definition and raises <c>ServiceUpdated</c>; an unchanged definition is a no-op.
    /// </summary>
    /// <param name="id">The service.</param>
    /// <param name="request">The new definition.</param>
    /// <param name="expectedVersion">The version the caller read, when it wants the change refused if someone changed the
    /// service since.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The service; <c>service.not_found</c>; <c>request.version_mismatch</c>; or a
    /// <see cref="ValidationError"/>.</returns>
    Task<Result<ServiceResponse>> UpdateAsync(Guid id, UpdateServiceRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a service.
    /// </summary>
    /// <returns>The service, or <c>service.not_found</c>.</returns>
    Task<Result<ServiceResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// A property's services, by name.
    /// </summary>
    Task<IReadOnlyList<ServiceResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
