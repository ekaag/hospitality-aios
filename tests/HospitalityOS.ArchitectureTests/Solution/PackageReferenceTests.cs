using HospitalityOS.ArchitectureTests.Support;
using Xunit;

namespace HospitalityOS.ArchitectureTests.Solution;

/// <summary>
/// Keeps vendor and infrastructure packages inside adapter projects (P5, solution-structure.md §2).
/// </summary>
public sealed class PackageReferenceTests
{
    /// <summary>
    /// Package ID prefixes that may only be referenced by projects under <c>src/Adapters/</c>.
    /// Extend this list whenever a new vendor or infrastructure SDK is introduced.
    /// </summary>
    private static readonly string[] adapterOnlyPackagePrefixes =
    [
        "Azure.",
        "Microsoft.Azure.",
        "AWSSDK.",
        "Google.Cloud.",
        "Anthropic",
        "OpenAI",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.Data.SqlClient",
        "Microsoft.Data.Sqlite",
        "StackExchange.Redis",
        "RabbitMQ.",
        "Confluent.Kafka",
    ];

    public static TheoryData<string> SourceProjectNames => new(SolutionModel.SourceProjects.Select(project => project.Name));

    [Theory]
    [MemberData(nameof(SourceProjectNames))]
    public void PackageReferences_OutsideAdapters_ExcludeVendorAndInfrastructureSdks(string projectName)
    {
        ProjectInfo project = SolutionModel.GetSourceProject(projectName);

        List<string> violations = project.Layer is Layer.Adapters
            ? []
            : project.PackageReferences
                .Where(package => adapterOnlyPackagePrefixes.Any(prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        Assert.True(
            violations.Count == 0,
            $"{project.Name} ({project.Layer}) references adapter-only packages: {string.Join(", ", violations)}");
    }

    [Theory]
    [MemberData(nameof(SourceProjectNames))]
    public void PackageReferences_InKernelDomainAndContractProjects_AreEmpty(string projectName)
    {
        ProjectInfo project = SolutionModel.GetSourceProject(projectName);
        bool mustBeDependencyFree = project.Name == SolutionModel.SharedKernelName
            || project.Layer is Layer.Domains or Layer.Contracts;

        Assert.True(
            !mustBeDependencyFree || project.PackageReferences.Count == 0,
            $"{project.Name} must not reference packages, but references: {string.Join(", ", project.PackageReferences)}");
    }

    [Theory]
    [MemberData(nameof(SourceProjectNames))]
    public void PackageReferences_InPortProjects_AreOnlyMicrosoftExtensionsAbstractions(string projectName)
    {
        ProjectInfo project = SolutionModel.GetSourceProject(projectName);
        bool isPortProject = project.Layer is Layer.Core && project.Name != SolutionModel.SharedKernelName;

        List<string> violations = isPortProject
            ? project.PackageReferences
                .Where(package => !(package.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
                    && package.EndsWith(".Abstractions", StringComparison.Ordinal)))
                .ToList()
            : [];

        Assert.True(
            violations.Count == 0,
            $"{project.Name} may reference only Microsoft.Extensions.*.Abstractions packages, but references: {string.Join(", ", violations)}");
    }

    [Theory]
    [MemberData(nameof(SourceProjectNames))]
    public void Sdk_OutsideHosts_IsPlainDotNetSdk(string projectName)
    {
        ProjectInfo project = SolutionModel.GetSourceProject(projectName);

        if (project.Layer is not Layer.Hosts)
        {
            Assert.Equal("Microsoft.NET.Sdk", project.Sdk);
        }
    }
}
