using HospitalityOS.Contracts.Api.Policies;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Policies;

/// <summary>
/// Drafts, edits, activates, retires, and reads the policies of the caller's tenant's properties (Journey 4). Commands
/// commit their change and its audit record together; rejected commands are audited too. Scoped; the caller's tenant
/// and actor come from the operation.
/// </summary>
public interface IPolicyAdministration
{
    /// <summary>
    /// Drafts a policy for a property and raises <c>PolicyCreated</c>.
    /// </summary>
    /// <returns>The draft, or a <see cref="ValidationError"/> listing every invalid field (a subject not in the property
    /// is <c>appliesTo.id</c>).</returns>
    Task<Result<PolicyResponse>> CreateAsync(CreatePolicyRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a policy's content and raises <c>PolicyUpdated</c>; unchanged content is a no-op.
    /// </summary>
    /// <param name="id">The policy.</param>
    /// <param name="request">The new content.</param>
    /// <param name="expectedVersion">The version the caller read, when it wants the change refused if someone changed the
    /// policy since.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The policy; <c>policy.not_found</c>; <c>request.version_mismatch</c>; <c>policy.retired</c>; or a
    /// <see cref="ValidationError"/>.</returns>
    Task<Result<PolicyResponse>> UpdateAsync(Guid id, UpdatePolicyRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Puts a policy in force and raises <c>PolicyUpdated</c>; activating an active policy is a no-op.
    /// </summary>
    /// <returns>The policy; <c>policy.not_found</c>; <c>request.version_mismatch</c>; or <c>policy.retired</c>.</returns>
    Task<Result<PolicyResponse>> ActivateAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a policy out of force for good and raises <c>PolicyUpdated</c>; retiring twice is a no-op.
    /// </summary>
    /// <returns>The policy; <c>policy.not_found</c>; or <c>request.version_mismatch</c>.</returns>
    Task<Result<PolicyResponse>> RetireAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a policy.
    /// </summary>
    /// <returns>The policy, or <c>policy.not_found</c>.</returns>
    Task<Result<PolicyResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// A property's policies in every status, by title.
    /// </summary>
    Task<IReadOnlyList<PolicyResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
