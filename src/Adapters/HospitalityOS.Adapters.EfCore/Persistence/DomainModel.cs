using System.Reflection;
using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Persistence;

/// <summary>
/// The assemblies holding persisted types, scanned for the ID and enum conventions.
/// </summary>
internal static class DomainModel
{
    private static readonly Assembly[] assemblies =
    [
        typeof(ITypedId).Assembly,
        typeof(Organization).Assembly,
        typeof(Property).Assembly,
        typeof(Menu).Assembly,
        typeof(PropertyEvent).Assembly,
        typeof(Service).Assembly,
        typeof(HumanAction).Assembly,
        typeof(Guest).Assembly,
    ];

    public static IEnumerable<Type> TypedIdTypes => Types.Where(type => type.IsValueType && typeof(ITypedId).IsAssignableFrom(type));

    public static IEnumerable<Type> EnumTypes => Types.Where(type => type.IsEnum);

    private static IEnumerable<Type> Types => assemblies.SelectMany(assembly => assembly.GetTypes()).Where(type => type.IsPublic);
}
