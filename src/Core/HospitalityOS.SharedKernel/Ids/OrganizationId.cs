namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// Identifies an organization. Lives in SharedKernel, not its domain project, because other domains
/// and the event envelope reference it and domain projects may not reference each other (P9).
/// </summary>
public readonly record struct OrganizationId : ITypedId
{
    /// <summary>
    /// Creates an organization ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public OrganizationId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(OrganizationId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
