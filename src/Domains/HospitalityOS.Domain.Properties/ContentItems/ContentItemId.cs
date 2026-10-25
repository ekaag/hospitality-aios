using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.ContentItems;

/// <summary>
/// Identifies a <see cref="ContentItem"/>.
/// </summary>
public readonly record struct ContentItemId : ITypedId
{
    /// <summary>
    /// Creates a content item ID.
    /// </summary>
    /// <param name="value">The identifier; must not be <see cref="Guid.Empty"/>.</param>
    public ContentItemId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(ContentItemId));
    }

    /// <inheritdoc />
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
