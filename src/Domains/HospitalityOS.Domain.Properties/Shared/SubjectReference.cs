namespace HospitalityOS.Domain.Properties.Shared;

/// <summary>
/// Points at the thing a record is about — whose hours, which amenity a policy governs, what content
/// describes. Referenced by kind and raw ID because subjects such as restaurants live in other domain
/// projects, which this one may not reference (P9). Same-property checks happen in the application layer.
/// </summary>
/// <param name="Kind">What kind of subject.</param>
/// <param name="Id">The subject's ID value, e.g. a <c>SpaceId</c>'s <c>Value</c>.</param>
public sealed record SubjectReference(SubjectKind Kind, Guid Id);
