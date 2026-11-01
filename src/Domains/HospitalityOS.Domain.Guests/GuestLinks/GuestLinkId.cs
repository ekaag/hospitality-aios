using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.GuestLinks;

/// <summary>
/// Identifies a <see cref="GuestLink"/>.
/// </summary>
public readonly record struct GuestLinkId : ITypedId
{
    /// <summary>
    /// Creates a guest link ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public GuestLinkId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(GuestLinkId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
