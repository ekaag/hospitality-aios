using HospitalityOS.ArchitectureTests.Support;
using Xunit;

namespace HospitalityOS.ArchitectureTests.Solution;

/// <summary>
/// Enforces dependency direction between layers (P9, solution-structure.md §2).
/// </summary>
public sealed class ProjectReferenceTests
{
    public static TheoryData<string> SourceProjectNames => new(SolutionModel.SourceProjects.Select(project => project.Name));

    [Theory]
    [MemberData(nameof(SourceProjectNames))]
    public void ProjectReferences_OfEachSourceProject_FollowLayerRules(string projectName)
    {
        ProjectInfo project = SolutionModel.GetSourceProject(projectName);

        List<string> violations = project.ProjectReferences
            .Select(referencePath => DescribeViolation(project, referencePath))
            .OfType<string>()
            .ToList();

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static string? DescribeViolation(ProjectInfo project, string referencePath)
    {
        string referenceName = Path.GetFileNameWithoutExtension(referencePath);
        ProjectInfo? reference = SolutionModel.SourceProjects.SingleOrDefault(candidate => candidate.Name == referenceName);

        if (reference is null)
        {
            return $"{project.Name} references {referenceName}, which is not a source project under src/ (production code must never reference tests).";
        }

        if (project.Layer is null || reference.Layer is null)
        {
            // Reported by ProjectNamingTests; avoid duplicate noise here.
            return null;
        }

        return IsAllowed(project, reference)
            ? null
            : $"{project.Name} ({project.Layer}) must not reference {reference.Name} ({reference.Layer}).";
    }

    // Only persistence adapters implement the repository interfaces declared in domain projects
    // (solution-structure.md §2); every other adapter sees ports and canonical types only.
    private static bool IsPersistenceAdapter(ProjectInfo project)
    {
        return project.Name == "HospitalityOS.Adapters.InMemory"
            || project.Name == "HospitalityOS.Adapters.EfCore"
            || project.Name.StartsWith("HospitalityOS.Adapters.EfCore.", StringComparison.Ordinal);
    }

    private static bool IsAllowed(ProjectInfo from, ProjectInfo to)
    {
        if (to.Layer is Layer.Hosts)
        {
            return false;
        }

        return from.Layer switch
        {
            Layer.Core or Layer.Domains or Layer.Contracts => to.Name == SolutionModel.SharedKernelName,
            Layer.Platform => to.Layer is Layer.Core or Layer.Domains or Layer.Contracts or Layer.Platform,
            Layer.Adapters => to.Layer is Layer.Core or Layer.Contracts or Layer.Adapters
                || (to.Layer is Layer.Domains && IsPersistenceAdapter(from)),
            Layer.Hosts => true,
            _ => false,
        };
    }
}
