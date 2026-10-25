namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// Identifies a guest conversation. Lives in SharedKernel because service requests and AI traces reference the conversation they came from, and domain projects may not reference
/// each other (P9).
/// </summary>
public readonly record struct ConversationId : ITypedId
{
    /// <summary>
    /// Creates the ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public ConversationId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(ConversationId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
