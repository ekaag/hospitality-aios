namespace HospitalityOS.Domain.Operations.HumanActions;

/// <summary>
/// Something to bring or restock, as staff see it, e.g. "2 × Bath towel".
/// </summary>
/// <param name="Name">What, e.g. <c>Bath towel</c>; required.</param>
/// <param name="Quantity">How many; at least 1.</param>
/// <param name="Code">The service item code, e.g. <c>bath_towel</c>, when it came from a service request; optional.</param>
public sealed record ActionItem(string Name, int Quantity, string? Code = null);
