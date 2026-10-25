using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.SharedKernel.Locations;

/// <summary>
/// Where physical work happens, as staff should see it (e.g. "Room 412"), plus the canonical IDs behind it.
/// Shared by service requests and human actions, which live in different domains.
/// </summary>
/// <param name="Label">What staff see, e.g. <c>Room 412</c> or <c>Pool deck</c>; required.</param>
/// <param name="AccommodationUnitId">The room, when the work is in a guest unit.</param>
/// <param name="SpaceId">The space, when known.</param>
public sealed record ActionLocation(string Label, AccommodationUnitId? AccommodationUnitId = null, SpaceId? SpaceId = null);
