using HospitalityOS.Contracts.Api.Stays;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.GuestLinks;

/// <summary>
/// The front desk's view of stays and their check-in links (K4c): the property's reserved and in-house stays, and a
/// stay's link created (replacing any open one) or revoked. Changes commit with their audit record. Scoped; the caller's
/// tenant and actor come from the operation.
/// </summary>
public interface IGuestLinks
{
    /// <summary>
    /// The property's reserved and in-house stays, by arrival.
    /// </summary>
    Task<IReadOnlyList<StayResponse>> ListStaysAsync(PropertyId propertyId, CancellationToken cancellationToken);

    /// <summary>
    /// A new link for the stay, revoking its open one; the code is in the result only.
    /// </summary>
    /// <returns>The stay and the code; <c>stay.not_found</c>; or <c>guest_link.stay_ended</c>.</returns>
    Task<Result<CreatedGuestLink>> CreateAsync(Guid stayId, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes the stay's open link, so its code opens nothing and tokens it gave stop working; none open is a no-op.
    /// </summary>
    /// <returns>The stay, or <c>stay.not_found</c>.</returns>
    Task<Result<StayResponse>> RevokeAsync(Guid stayId, CancellationToken cancellationToken);
}

/// <summary>
/// A new link's stay and code.
/// </summary>
public sealed record CreatedGuestLink(StayContextId StayId, string Code);
