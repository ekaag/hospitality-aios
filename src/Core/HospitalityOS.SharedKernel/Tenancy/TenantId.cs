using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.SharedKernel.Tenancy;

/// <summary>
/// Identifies the tenant that owns data. Every tenant-owned aggregate carries one, and
/// persistence adapters filter on it (P10).
/// </summary>
public readonly record struct TenantId : ITypedId
{
    /// <summary>
    /// Creates a tenant ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public TenantId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(TenantId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
