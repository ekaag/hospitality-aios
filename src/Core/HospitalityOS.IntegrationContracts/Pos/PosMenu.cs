using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.IntegrationContracts.Pos;

/// <summary>
/// One of an outlet's menus as the POS reports it; the runtime maps it to a <c>Menu</c>.
/// </summary>
/// <param name="Id">The provider's ID for the menu.</param>
/// <param name="Name">The menu's name, e.g. <c>Dinner</c>.</param>
/// <param name="Items">The menu's items.</param>
public sealed record PosMenu(ExternalId Id, string Name, ValueList<PosMenuItem> Items);
