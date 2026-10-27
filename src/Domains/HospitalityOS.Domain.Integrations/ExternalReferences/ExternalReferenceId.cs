using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Integrations.ExternalReferences;

/// <summary>
/// Identifies an <see cref="ExternalReference"/>.
/// </summary>
public readonly record struct ExternalReferenceId : ITypedId
{
    /// <summary>
    /// Creates an ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public ExternalReferenceId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(ExternalReferenceId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
