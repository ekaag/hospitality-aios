using System.Reflection;
using Xunit;

namespace HospitalityOS.ArchitectureTests.Types;

/// <summary>
/// Keeps the hospitality integration ports to the spec §10 set and their signatures to canonical types (P3, P5):
/// a vendor, transport, or serialization type in a connector contract would leak a provider into every caller.
/// </summary>
public sealed class IntegrationContractTests
{
    private const string ProjectName = "HospitalityOS.IntegrationContracts";

    private static readonly string[] specConnectors =
    [
        "ICmsConnector",
        "ICrsConnector",
        "IMessagingConnector",
        "IPaymentConnector",
        "IPmsConnector",
        "IPosConnector",
    ];

    private static readonly string[] canonicalNamespacePrefixes = ["HospitalityOS.SharedKernel.", ProjectName + "."];

    private static readonly string[] allowedSystemNamespaces =
    [
        "System",
        "System.Collections.Generic",
        "System.Threading",
        "System.Threading.Tasks",
    ];

    private const BindingFlags PublicDeclared =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static Assembly Contracts => Assembly.Load(new AssemblyName(ProjectName));

    [Fact]
    public void Interfaces_AreExactlyTheSpecConnectors()
    {
        IEnumerable<string> interfaces = Contracts.GetExportedTypes()
            .Where(type => type.IsInterface)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(specConnectors, interfaces);
    }

    [Fact]
    public void PublicSignatures_UseOnlyCanonicalTypes()
    {
        List<string> violations = Contracts.GetExportedTypes()
            .SelectMany(type => SignatureTypes(type)
                .SelectMany(Unwrap)
                .Where(used => !IsCanonical(used))
                .Select(used => $"{type.FullName} exposes {used.FullName}"))
            .Distinct()
            .ToList();

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ConnectorMethods_AreAsyncAndTakeCancellationTokenLast()
    {
        List<string> violations = Contracts.GetExportedTypes()
            .Where(type => type.IsInterface)
            .SelectMany(type => type.GetMethods())
            .Where(method => !typeof(Task).IsAssignableFrom(method.ReturnType)
                || method.GetParameters().LastOrDefault()?.ParameterType != typeof(CancellationToken))
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
            .ToList();

        Assert.True(violations.Count == 0, "Connector methods must return a Task and end with a CancellationToken: " + string.Join(", ", violations));
    }

    private static IEnumerable<Type> SignatureTypes(Type type)
    {
        foreach (MemberInfo member in type.GetMembers(PublicDeclared))
        {
            switch (member)
            {
                case MethodBase method:
                    if (method is MethodInfo { ReturnType: Type returnType })
                    {
                        yield return returnType;
                    }

                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        yield return parameter.ParameterType;
                    }

                    break;
                case PropertyInfo property:
                    yield return property.PropertyType;
                    break;
                case FieldInfo field:
                    yield return field.FieldType;
                    break;
                case EventInfo { EventHandlerType: Type handlerType }:
                    yield return handlerType;
                    break;
            }
        }
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.HasElementType)
        {
            return Unwrap(type.GetElementType()!); // HasElementType guarantees an element type.
        }

        if (type.IsGenericParameter)
        {
            return [];
        }

        return type.IsConstructedGenericType
            ? type.GetGenericArguments().SelectMany(Unwrap).Prepend(type.GetGenericTypeDefinition())
            : [type];
    }

    private static bool IsCanonical(Type type)
    {
        string space = type.Namespace ?? "";

        return allowedSystemNamespaces.Contains(space)
            || canonicalNamespacePrefixes.Any(prefix => (space + ".").StartsWith(prefix, StringComparison.Ordinal));
    }
}
