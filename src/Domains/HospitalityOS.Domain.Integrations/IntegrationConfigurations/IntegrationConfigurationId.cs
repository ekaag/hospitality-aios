using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Integrations.IntegrationConfigurations;

/// <summary>
/// Identifies an <see cref="IntegrationConfiguration"/>.
/// </summary>
public readonly record struct IntegrationConfigurationId : ITypedId
{
    /// <summary>
    /// Creates an ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public IntegrationConfigurationId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(IntegrationConfigurationId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
