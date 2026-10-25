namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// Identifies a guest. Lives in SharedKernel because service requests, notifications, and traces reference guests, and domain projects may not reference
/// each other (P9).
/// </summary>
public readonly record struct GuestId : ITypedId
{
    /// <summary>
    /// Creates the ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public GuestId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(GuestId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
