using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.IntegrationContracts.Pos;

/// <summary>
/// A dish or drink as the POS reports it; the runtime maps it to a <c>MenuItem</c>.
/// </summary>
/// <param name="Id">The provider's ID for the item.</param>
/// <param name="Name">The item's name.</param>
/// <param name="Description">The item's description; optional.</param>
/// <param name="Category">The provider's section, e.g. <c>Starters</c>; optional.</param>
/// <param name="Price">The current price.</param>
/// <param name="IsAvailable">Whether the item can be ordered now (e.g. not sold out).</param>
public sealed record PosMenuItem(
    ExternalId Id,
    string Name,
    string? Description,
    string? Category,
    Money Price,
    bool IsAvailable);
