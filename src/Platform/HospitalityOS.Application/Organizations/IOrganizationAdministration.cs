using HospitalityOS.Contracts.Api.Organizations;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Organizations;

/// <summary>
/// Creates, renames, and reads the caller's tenant's organizations. Commands commit their change and its audit record
/// together; rejected commands are audited too. Scoped; the caller's tenant and actor come from the operation.
/// </summary>
public interface IOrganizationAdministration
{
    /// <summary>
    /// Creates an organization and raises <c>OrganizationCreated</c>.
    /// </summary>
    /// <returns>The organization, or a <see cref="ValidationError"/>.</returns>
    Task<Result<OrganizationResponse>> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Renames an organization; the same name is a no-op.
    /// </summary>
    /// <param name="id">The organization.</param>
    /// <param name="request">The new name.</param>
    /// <param name="expectedVersion">The version the caller read, when it wants the change refused if someone changed the
    /// organization since.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The organization; <c>organization.not_found</c>; <c>request.version_mismatch</c>; or a
    /// <see cref="ValidationError"/>.</returns>
    Task<Result<OrganizationResponse>> RenameAsync(OrganizationId id, RenameOrganizationRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Reads an organization.
    /// </summary>
    /// <returns>The organization, or <c>organization.not_found</c>.</returns>
    Task<Result<OrganizationResponse>> GetAsync(OrganizationId id, CancellationToken cancellationToken);

    /// <summary>
    /// The tenant's organizations, by name.
    /// </summary>
    Task<IReadOnlyList<OrganizationResponse>> ListAsync(CancellationToken cancellationToken);
}
