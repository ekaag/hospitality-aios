using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Dining.Menus;

/// <summary>
/// Identifies a <see cref="MenuItem"/> within its menu; stable across menu edits so answers can cite it.
/// </summary>
public readonly record struct MenuItemId : ITypedId
{
    /// <summary>
    /// Creates a menu item ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public MenuItemId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(MenuItemId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
