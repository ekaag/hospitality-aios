using HospitalityOS.Contracts.Capabilities;

namespace HospitalityOS.Contracts.Api.Properties;

/// <summary>
/// The thing a record is about: whose hours, which amenity a policy governs, what content describes. It must exist in
/// the same property.
/// </summary>
/// <param name="Kind">What kind of thing; required.</param>
/// <param name="Id">Its ID (for <see cref="SubjectKind.Property"/>, the property's own); required.</param>
public sealed record SubjectReference(
    SubjectKind? Kind = null,
    Guid? Id = null);
