using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>GuestRegistered</c>: a guest was registered. Carries no personal data (P10).
/// </summary>
/// <param name="GuestId">The guest.</param>
public sealed record GuestRegisteredV1(
    GuestId GuestId) : IEventPayload;
