using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>GuestProfileUpdated</c>: a guest's profile changed. Carries no personal data (P10).
/// </summary>
/// <param name="GuestId">The guest.</param>
public sealed record GuestProfileUpdatedV1(
    GuestId GuestId) : IEventPayload;
