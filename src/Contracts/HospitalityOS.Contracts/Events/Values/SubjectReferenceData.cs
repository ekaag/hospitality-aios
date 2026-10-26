namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// What a record is about, e.g. an amenity's hours or a policy for the gym.
/// </summary>
/// <param name="Kind">Property, space, amenity, restaurant, or service (domain enum name).</param>
/// <param name="Id">The subject's ID.</param>
public sealed record SubjectReferenceData(
    string Kind,
    Guid Id);
