using System.Reflection;
using HospitalityOS.ArchitectureTests.Support;
using Xunit;

namespace HospitalityOS.ArchitectureTests.Types;

/// <summary>
/// Enforces type-level dependency rules on compiled assemblies (P5, P7, P9; coding-standards.md §3).
/// These complement the project-graph checks and become meaningful as code is added.
/// </summary>
public sealed class TypeDependencyTests
{
    private static readonly string[] vendorNamespaces =
    [
        "Azure",
        "Amazon",
        "Google.Cloud",
        "Anthropic",
        "OpenAI",
        "Npgsql",
        "StackExchange.Redis",
        "RabbitMQ",
        "Confluent.Kafka",
    ];

    private static readonly string[] domainForbiddenNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.DependencyInjection",
        "System.Text.Json.Serialization",
        "System.Net.Http",
    ];

    [Fact]
    public void Types_OutsideAdapters_DoNotDependOnVendorSdks()
    {
        IEnumerable<Assembly> assemblies = SolutionModel.LoadSourceAssemblies(project => project.Layer is not Layer.Adapters);

        AssertNoDependencies(assemblies, vendorNamespaces);
    }

    [Fact]
    public void Types_OutsideAdapters_DoNotDependOnEntityFramework()
    {
        IEnumerable<Assembly> assemblies = SolutionModel.LoadSourceAssemblies(project => project.Layer is not Layer.Adapters and not Layer.Hosts);

        AssertNoDependencies(assemblies, ["Microsoft.EntityFrameworkCore"]);
    }

    [Fact]
    public void Types_InDomains_DependOnNoInfrastructure()
    {
        IEnumerable<Assembly> assemblies = SolutionModel.LoadSourceAssemblies(project => project.Layer is Layer.Domains);

        AssertNoDependencies(assemblies, [.. domainForbiddenNamespaces, .. vendorNamespaces]);
    }

    private static void AssertNoDependencies(IEnumerable<Assembly> assemblies, string[] forbiddenNamespaces)
    {
        NetArchTest.Rules.TestResult result = NetArchTest.Rules.Types
            .InAssemblies(assemblies)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    private static string FormatFailures(NetArchTest.Rules.TestResult result)
    {
        return "Forbidden dependencies found in: " + string.Join(", ", result.FailingTypeNames ?? []);
    }
}
