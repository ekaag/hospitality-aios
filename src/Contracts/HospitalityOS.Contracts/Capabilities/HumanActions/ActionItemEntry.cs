using System.ComponentModel;

namespace HospitalityOS.Contracts.Capabilities.HumanActions;

/// <summary>
/// Something to bring or restock, e.g. 2 × Bath towel.
/// </summary>
/// <param name="Name">What, as staff read it.</param>
/// <param name="Quantity">How many.</param>
/// <param name="Code">The service item code, when it came from a service request.</param>
public sealed record ActionItemEntry(
    [property: Description("What, as staff read it, e.g. Bath towel.")] string Name,
    [property: Description("How many; at least 1.")] int Quantity,
    [property: Description("The service item code, e.g. bath_towel, when it came from a service request.")] string? Code = null);
