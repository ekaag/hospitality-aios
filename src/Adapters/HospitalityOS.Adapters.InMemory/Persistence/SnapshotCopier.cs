using System.Reflection;
using System.Runtime.CompilerServices;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// Deep-copies aggregates field by field, so the store never shares an object with a caller: changing a loaded
/// aggregate without committing cannot leak into the store or another scope, just as with a database.
/// Reflection keeps the domain free of copy methods and persistence concerns.
/// </summary>
internal static class SnapshotCopier
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public static T Copy<T>(T source)
        where T : class
    {
        return (T)CopyValue(source, new Dictionary<object, object>(ReferenceEqualityComparer.Instance))!;
    }

    private static object? CopyValue(object? source, Dictionary<object, object> copies)
    {
        if (source is null || IsImmutable(source.GetType()))
        {
            return source;
        }

        if (copies.TryGetValue(source, out object? existing))
        {
            return existing;
        }

        return source switch
        {
            Array array => CopyArray(array, copies),
            Delegate => throw new NotSupportedException($"Aggregates may not hold delegates ({source.GetType()})."),
            _ => CopyObject(source, copies),
        };
    }

    private static Array CopyArray(Array source, Dictionary<object, object> copies)
    {
        if (source.Rank != 1)
        {
            throw new NotSupportedException("Only single-dimensional arrays are supported.");
        }

        Array copy = (Array)source.Clone();
        copies[source] = copy;

        for (int index = 0; index < source.Length; index++)
        {
            copy.SetValue(CopyValue(source.GetValue(index), copies), index);
        }

        return copy;
    }

    private static object CopyObject(object source, Dictionary<object, object> copies)
    {
        Type type = source.GetType();
        object copy = RuntimeHelpers.GetUninitializedObject(type);

        if (!type.IsValueType)
        {
            // Registered before copying fields so shared references and cycles copy once.
            copies[source] = copy;
        }

        for (Type? current = type; current is not null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(InstanceFields))
            {
                field.SetValue(copy, CopyValue(field.GetValue(source), copies));
            }
        }

        return copy;
    }

    private static bool IsImmutable(Type type)
    {
        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(Guid)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
            || type == typeof(TimeSpan);
    }
}
