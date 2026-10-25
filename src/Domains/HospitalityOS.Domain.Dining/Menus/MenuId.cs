using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Dining.Menus;

/// <summary>
/// Identifies a <see cref="Menu"/>.
/// </summary>
public readonly record struct MenuId : ITypedId
{
    /// <summary>
    /// Creates a menu ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public MenuId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(MenuId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
