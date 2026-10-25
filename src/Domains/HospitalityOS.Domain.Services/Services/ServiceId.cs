using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Services.Services;

/// <summary>
/// Identifies a <see cref="Service"/>.
/// </summary>
public readonly record struct ServiceId : ITypedId
{
    /// <summary>
    /// Creates a service ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public ServiceId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(ServiceId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
