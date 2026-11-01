using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Organizations.Access;

/// <summary>
/// Identifies a <see cref="StaffAccess"/>.
/// </summary>
public readonly record struct StaffAccessId : ITypedId
{
    /// <summary>
    /// Creates a staff access ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public StaffAccessId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(StaffAccessId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
