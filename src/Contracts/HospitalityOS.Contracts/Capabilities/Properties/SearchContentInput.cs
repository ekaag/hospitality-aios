using System.ComponentModel;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// Input of <c>Property.SearchContent</c>.
/// </summary>
/// <param name="PropertyId">The property.</param>
/// <param name="Query">Words to look for, e.g. <c>wifi password</c>.</param>
/// <param name="Type">Only content of this kind.</param>
/// <param name="Limit">Most results to return, 1 to 20.</param>
public sealed record SearchContentInput(
    [property: Description("The property.")] PropertyId PropertyId,
    [property: Description("Words to look for, e.g. wifi password.")] string Query,
    [property: Description("Only content of this kind.")] ContentType? Type = null,
    [property: Description("Most results to return, 1 to 20.")] int Limit = 5);
