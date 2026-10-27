namespace HospitalityOS.IntegrationContracts;

/// <summary>
/// An identifier issued by an external provider (a PMS reservation number, a POS item code, a message ID), kept exactly
/// as issued and compared ordinally. It never replaces a canonical platform ID (P3): the integration runtime maps the
/// two through <c>ExternalReference</c> (E2), so swapping providers leaves canonical IDs unchanged (Journey 18).
/// </summary>
public readonly record struct ExternalId
{
    /// <summary>
    /// Longest identifier accepted from a provider.
    /// </summary>
    public const int MaxLength = 256;

    /// <summary>
    /// Creates an external ID.
    /// </summary>
    /// <param name="value">The provider's identifier; must not be blank or longer than <see cref="MaxLength"/>.</param>
    public ExternalId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, MaxLength, nameof(value));

        Value = value;
    }

    /// <summary>
    /// The provider's identifier, unchanged.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
