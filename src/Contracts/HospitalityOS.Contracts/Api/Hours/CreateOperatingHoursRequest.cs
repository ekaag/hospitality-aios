using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Hours;

/// <summary>
/// <c>POST /api/v1/operating-hours</c>: sets when something is open (Journey 2): weekly hours, date overrides, and
/// temporary closures. A subject has at most one operating hours record; change it with <c>PUT</c>.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="PropertyId">The property; required.</param>
/// <param name="Subject">Whose hours: the property, or one of its spaces, amenities, restaurants, or services; required.</param>
/// <param name="Weekly">The usual hours per day of the week.</param>
/// <param name="Overrides">Dates with different hours.</param>
/// <param name="Closures">Date ranges when it is closed.</param>
public sealed record CreateOperatingHoursRequest(
    PropertyId? PropertyId = null,
    SubjectReference? Subject = null,
    ValueList<WeeklyHours>? Weekly = null,
    ValueList<HoursOverride>? Overrides = null,
    ValueList<HoursClosure>? Closures = null);
