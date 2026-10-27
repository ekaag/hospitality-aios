using System.ComponentModel;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// Input of <c>Property.GetProperty</c>.
/// </summary>
/// <param name="PropertyId">The property.</param>
public sealed record GetPropertyInput(
    [property: Description("The property.")] PropertyId PropertyId);
