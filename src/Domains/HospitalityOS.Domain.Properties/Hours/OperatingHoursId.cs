using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Hours;

/// <summary>
/// Identifies an <see cref="OperatingHours"/> record.
/// </summary>
public readonly record struct OperatingHoursId : ITypedId
{
    /// <summary>
    /// Creates an operating hours ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public OperatingHoursId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(OperatingHoursId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
