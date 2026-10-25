using System.Collections;
using System.Runtime.CompilerServices;

namespace HospitalityOS.SharedKernel.Collections;

/// <summary>
/// An immutable list with value equality, so records holding lists compare by content
/// (needed to detect no-op updates and to compare events in tests).
/// </summary>
/// <typeparam name="T">The element type; should itself have value equality.</typeparam>
[CollectionBuilder(typeof(ValueList), nameof(ValueList.Create))]
public sealed class ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
{
    private readonly T[] items;

    internal ValueList(T[] items)
    {
        this.items = items;
    }

    internal static ValueList<T> Empty { get; } = new([]);

    /// <inheritdoc />
    public int Count => items.Length;

    /// <inheritdoc />
    public T this[int index] => items[index];

    public static bool operator ==(ValueList<T>? left, ValueList<T>? right) => Equals(left, right);

    public static bool operator !=(ValueList<T>? left, ValueList<T>? right) => !Equals(left, right);

    /// <inheritdoc />
    public bool Equals(ValueList<T>? other) =>
        other is not null && (ReferenceEquals(this, other) || items.AsSpan().SequenceEqual(other.items));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ValueList<T>);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();

        foreach (T item in items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

    /// <inheritdoc />
    public override string ToString() => $"[{string.Join(", ", items)}]";
}
