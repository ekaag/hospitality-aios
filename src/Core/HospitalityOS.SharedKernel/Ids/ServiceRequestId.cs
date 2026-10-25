namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// Identifies a guest service request. Lives in SharedKernel because human actions and guest status lookups reference the request they fulfil, and domain projects may not reference
/// each other (P9).
/// </summary>
public readonly record struct ServiceRequestId : ITypedId
{
    /// <summary>
    /// Creates the ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public ServiceRequestId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(ServiceRequestId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
