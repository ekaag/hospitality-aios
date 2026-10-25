using System.Reflection;
using System.Runtime.CompilerServices;
using HospitalityOS.ArchitectureTests.Support;
using Xunit;

namespace HospitalityOS.ArchitectureTests.Types;

/// <summary>
/// Enforces P15: constructor injection of ports, no service locator, no raw configuration in business
/// code, and registrations only in each project's <c>DependencyInjection</c> class.
/// </summary>
public sealed class DependencyInjectionTests
{
    public const string RegistrationClassName = "DependencyInjection";

    /// <summary>
    /// The binding mechanism resolves adapters from the container and reads their configuration by design (A3).
    /// </summary>
    private const string BindingsProjectName = "HospitalityOS.Bindings";

    private static readonly string[] containerAndConfigurationTypes =
    [
        "System.IServiceProvider",
        "Microsoft.Extensions.DependencyInjection.IServiceScopeFactory",
        "Microsoft.Extensions.DependencyInjection.IKeyedServiceProvider",
        "Microsoft.Extensions.Configuration.IConfiguration",
        "Microsoft.Extensions.Configuration.IConfigurationRoot",
        "Microsoft.Extensions.Configuration.IConfigurationSection",
    ];

    private static readonly string[] registrationBuilderTypes =
    [
        "Microsoft.Extensions.DependencyInjection.IServiceCollection",
        "Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder",
    ];

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static TheoryData<string> NonHostProjectNames => new(
        SolutionModel.SourceProjects.Where(project => project.Layer is not Layer.Hosts).Select(project => project.Name));

    [Theory]
    [MemberData(nameof(NonHostProjectNames))]
    public void Types_OutsideCompositionCode_DoNotHoldContainerOrConfiguration(string projectName)
    {
        if (projectName == BindingsProjectName)
        {
            return;
        }

        List<string> violations = LoadTypes(projectName)
            .Where(type => type.Name != RegistrationClassName)
            .SelectMany(DescribeContainerDependencies)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Inject ports (and IOptions<T> for settings) instead of the container or IConfiguration (P15):" + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [MemberData(nameof(NonHostProjectNames))]
    public void RegistrationExtensions_LiveOnlyInOnePublicDependencyInjectionClass(string projectName)
    {
        List<Type> declaringTypes = LoadTypes(projectName)
            .Where(type => type.GetMethods(AllDeclared).Any(IsRegistrationExtension))
            .ToList();

        List<string> violations = declaringTypes
            .Where(type => type.Name != RegistrationClassName || type.Namespace != projectName || !type.IsPublic)
            .Select(type => $"{type.FullName} declares IServiceCollection extensions; move them to public static {projectName}.{RegistrationClassName}.")
            .ToList();

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> DescribeContainerDependencies(Type type)
    {
        foreach (ConstructorInfo constructor in type.GetConstructors(AllDeclared))
        {
            foreach (ParameterInfo parameter in constructor.GetParameters().Where(parameter => IsForbidden(parameter.ParameterType)))
            {
                yield return $"{type.FullName} constructor parameter '{parameter.Name}' is {parameter.ParameterType.Name}.";
            }
        }

        foreach (FieldInfo field in type.GetFields(AllDeclared).Where(field => IsForbidden(field.FieldType)))
        {
            yield return $"{type.FullName} field '{field.Name}' is {field.FieldType.Name}.";
        }

        foreach (PropertyInfo property in type.GetProperties(AllDeclared).Where(property => IsForbidden(property.PropertyType)))
        {
            yield return $"{type.FullName} property '{property.Name}' is {property.PropertyType.Name}.";
        }
    }

    private static bool IsForbidden(Type type)
    {
        if (type.IsGenericType && type.GetGenericArguments().Any(IsForbidden))
        {
            return true;
        }

        Type candidate = type.HasElementType ? type.GetElementType()! : type;

        return containerAndConfigurationTypes.Contains(candidate.FullName, StringComparer.Ordinal);
    }

    private static bool IsRegistrationExtension(MethodInfo method)
    {
        if (!method.IsStatic || !method.IsDefined(typeof(ExtensionAttribute), inherit: false))
        {
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();

        return parameters.Length > 0 && registrationBuilderTypes.Contains(parameters[0].ParameterType.FullName, StringComparer.Ordinal);
    }

    private static IEnumerable<Type> LoadTypes(string projectName)
    {
        return SolutionModel
            .LoadSourceAssemblies(project => project.Name == projectName)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false));
    }
}
