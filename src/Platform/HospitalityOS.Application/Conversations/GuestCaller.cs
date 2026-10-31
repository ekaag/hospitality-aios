using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Application.Conversations;

/// <summary>
/// A guest calling with a stay token: who they are, which stay it is for, and at which property. The host reads it from
/// the validated token; the caller's tenant is the token's.
/// </summary>
/// <param name="GuestId">The guest.</param>
/// <param name="StayContextId">The stay the token was issued for.</param>
/// <param name="PropertyId">The property of the stay.</param>
public sealed record GuestCaller(GuestId GuestId, StayContextId StayContextId, PropertyId PropertyId)
{
    /// <summary>
    /// Whether <paramref name="stay"/> is the token's stay, the caller's at the token's property, and reserved or in house:
    /// what a guest needs to use the guest surface.
    /// </summary>
    public bool IsActive(StayContext? stay) =>
        stay is not null
        && stay.Id == StayContextId
        && stay.GuestId == GuestId
        && stay.PropertyId == PropertyId
        && stay.Status is StayStatus.Reserved or StayStatus.InHouse;
}
