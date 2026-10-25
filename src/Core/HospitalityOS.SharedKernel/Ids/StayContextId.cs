namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// Identifies a guest's stay (who is staying where, when). Lives in SharedKernel because service requests and status lookups reference stays, and domain projects may not reference
/// each other (P9).
/// </summary>
public readonly record struct StayContextId : ITypedId
{
    /// <summary>
    /// Creates the ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public StayContextId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(StayContextId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
