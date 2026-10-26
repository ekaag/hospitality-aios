using System.Linq.Expressions;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Conversions;

/// <summary>
/// Creates a strongly typed ID from its <see cref="Guid"/> through the ID's public constructor, compiled once per type.
/// </summary>
internal static class TypedIds<TId>
    where TId : struct, ITypedId
{
    private static readonly Func<Guid, TId> create = Compile();

    public static TId Create(Guid value) => create(value);

    private static Func<Guid, TId> Compile()
    {
        ParameterExpression value = Expression.Parameter(typeof(Guid), "value");
        System.Reflection.ConstructorInfo constructor = typeof(TId).GetConstructor([typeof(Guid)])
            ?? throw new InvalidOperationException($"{typeof(TId).Name} needs a public constructor taking a Guid.");

        return Expression.Lambda<Func<Guid, TId>>(Expression.New(constructor, value), value).Compile();
    }
}
