namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// Identifies a staff member who performs human actions. Lives in SharedKernel because operations, authorization, and audit reference staff, and domain projects may not reference
/// each other (P9).
/// </summary>
public readonly record struct StaffMemberId : ITypedId
{
    /// <summary>
    /// Creates the ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public StaffMemberId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(StaffMemberId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
