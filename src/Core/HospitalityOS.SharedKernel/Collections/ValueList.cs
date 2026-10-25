namespace HospitalityOS.SharedKernel.Collections;

/// <summary>
/// Creates <see cref="ValueList{T}"/> instances; also backs collection expressions (<c>ValueList&lt;int&gt; list = [1, 2];</c>).
/// </summary>
public static class ValueList
{
    /// <summary>
    /// Creates a list by copying <paramref name="items"/>.
    /// </summary>
    public static ValueList<T> Create<T>(ReadOnlySpan<T> items) => items.IsEmpty ? ValueList<T>.Empty : new ValueList<T>(items.ToArray());

    /// <summary>
    /// Creates a list by copying <paramref name="items"/>.
    /// </summary>
    public static ValueList<T> From<T>(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        T[] copy = [.. items];

        return copy.Length == 0 ? ValueList<T>.Empty : new ValueList<T>(copy);
    }
}
