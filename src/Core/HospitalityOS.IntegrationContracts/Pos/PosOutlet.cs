namespace HospitalityOS.IntegrationContracts.Pos;

/// <summary>
/// A place that sells food or drink, as the POS reports it; the runtime maps it to a <c>Restaurant</c>.
/// </summary>
/// <param name="Id">The provider's ID for the outlet.</param>
/// <param name="Name">The outlet's name.</param>
public sealed record PosOutlet(ExternalId Id, string Name);
