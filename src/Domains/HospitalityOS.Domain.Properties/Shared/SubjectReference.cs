namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// The thing whose hours are described. Referenced by kind and raw ID because subjects such as restaurants
/// live in other domain projects, which this one may not reference (P9).
/// </summary>
/// <param name="Kind">What kind of subject.</param>
/// <param name="Id">The subject's ID value, e.g. a <c>SpaceId</c>'s <c>Value</c>.</param>
public sealed record HoursSubject(HoursSubjectKind Kind, Guid Id);
