namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// Identifies one message in a guest conversation. Lives in SharedKernel because service requests, events, and AI traces cite the triggering message, and domain projects may not reference
/// each other (P9).
/// </summary>
public readonly record struct GuestMessageId : ITypedId
{
    /// <summary>
    /// Creates the ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public GuestMessageId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(GuestMessageId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
