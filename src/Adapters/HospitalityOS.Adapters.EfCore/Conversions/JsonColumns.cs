using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HospitalityOS.Adapters.EfCore.Conversions;

/// <summary>
/// Stores a value object as a JSON text column. Used for lists and for records nested inside another record,
/// which EF Core cannot map as columns because it cannot pass a nested record to its parent's constructor.
/// </summary>
internal static class JsonColumns
{
    /// <summary>
    /// For immutable values with value equality (records, <c>ValueList</c>), so change detection compares by value.
    /// </summary>
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property)
        where T : class?
    {
        return property.HasConversion(Converter<T>(), ImmutableComparer<T>());
    }

    /// <inheritdoc cref="HasJsonConversion{T}(PropertyBuilder{T})"/>
    public static ComplexTypePropertyBuilder<T> HasJsonConversion<T>(this ComplexTypePropertyBuilder<T> property)
        where T : class?
    {
        return property.HasConversion(Converter<T>(), ImmutableComparer<T>());
    }

    /// <summary>
    /// For a mutable list changed in place: change detection compares element by element against a copy.
    /// </summary>
    public static PropertyBuilder<List<T>> HasJsonListConversion<T>(this PropertyBuilder<List<T>> property)
    {
        ValueComparer<List<T>> comparer = new(
            (left, right) => left!.SequenceEqual(right!),
            list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            list => list.ToList());

        return property.HasConversion(Converter<List<T>>(), comparer);
    }

    private static ValueConverter<T, string> Converter<T>() =>
        new(value => PersistenceJson.Serialize(value), json => PersistenceJson.Deserialize<T>(json));

    private static ValueComparer<T> ImmutableComparer<T>() =>
        new((left, right) => Equals(left, right), value => value == null ? 0 : value.GetHashCode(), value => value);
}
