using HospitalityOS.ArchitectureTests.Support;
using Xunit;

namespace HospitalityOS.ArchitectureTests.Types;

/// <summary>
/// Adapters expose only their DI extension and options types; implementations are
/// <c>internal sealed</c> (solution-structure.md §4).
/// </summary>
public sealed class AdapterVisibilityTests
{
    public static TheoryData<string> AdapterProjectNames => new(
        SolutionModel.SourceProjects.Where(project => project.Layer is Layer.Adapters).Select(project => project.Name));

    [Theory]
    [MemberData(nameof(AdapterProjectNames))]
    public void PublicTypes_InAdapters_AreOnlyStaticExtensionsOrOptions(string projectName)
    {
        NetArchTest.Rules.TestResult result = TypesIn(projectName)
            .That()
            .ArePublic()
            .Should()
            .BeStatic()
            .Or()
            .HaveNameEndingWith("Options", StringComparison.Ordinal)
            .GetResult();

        Assert.True(result.IsSuccessful, "Public types must be static DI extensions or *Options: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(AdapterProjectNames))]
    public void ConcreteClasses_InAdapters_AreSealed(string projectName)
    {
        NetArchTest.Rules.TestResult result = TypesIn(projectName)
            .That()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .AreNotStatic()
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, "Concrete adapter classes must be sealed: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    private static NetArchTest.Rules.Types TypesIn(string projectName)
    {
        return NetArchTest.Rules.Types.InAssemblies(SolutionModel.LoadSourceAssemblies(project => project.Name == projectName));
    }
}
