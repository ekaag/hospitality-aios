using System.ComponentModel;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// Input of <c>Property.GetPolicy</c>.
/// </summary>
/// <param name="PropertyId">The property.</param>
/// <param name="Type">Only policies on this topic.</param>
/// <param name="SubjectId">Only policies for this amenity, restaurant, space, or service, plus property-wide ones.</param>
public sealed record GetPolicyInput(
    [property: Description("The property.")] PropertyId PropertyId,
    [property: Description("Only policies on this topic.")] PolicyType? Type = null,
    [property: Description("Only policies for this amenity, restaurant, space, or service, plus property-wide ones.")] Guid? SubjectId = null);
