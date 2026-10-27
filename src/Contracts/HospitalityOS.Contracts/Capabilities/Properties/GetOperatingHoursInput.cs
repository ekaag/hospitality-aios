using System.ComponentModel;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// Input of <c>Property.GetOperatingHours</c>.
/// </summary>
/// <param name="PropertyId">The property.</param>
/// <param name="Subject">Whose hours: the property itself, or an amenity, restaurant, space, or service.</param>
/// <param name="SubjectId">The amenity, restaurant, space, or service ID; required unless the subject is the property.</param>
/// <param name="Date">The property-local date; today when omitted.</param>
public sealed record GetOperatingHoursInput(
    [property: Description("The property.")] PropertyId PropertyId,
    [property: Description("Whose hours: the property itself, or an amenity, restaurant, space, or service.")] SubjectKind Subject = SubjectKind.Property,
    [property: Description("The amenity, restaurant, space, or service ID; required unless the subject is the property.")] Guid? SubjectId = null,
    [property: Description("The property-local date; today when omitted.")] DateOnly? Date = null);
