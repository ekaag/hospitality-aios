using HospitalityOS.ArchitectureTests.Support;
using Xunit;

namespace HospitalityOS.ArchitectureTests.Solution;

/// <summary>
/// Enforces project placement and naming (solution-structure.md §2–§3).
/// </summary>
public sealed class ProjectNamingTests
{
    public static TheoryData<string> SourceProjectNames => new(SolutionModel.SourceProjects.Select(project => project.Name));

    [Theory]
    [MemberData(nameof(SourceProjectNames))]
    public void LayerFolder_OfEachSourceProject_IsKnownLayer(string projectName)
    {
        ProjectInfo project = SolutionModel.GetSourceProject(projectName);

        Assert.True(
            project.Layer is not null,
            $"{project.Name} is under src/{project.LayerFolder}/, which is not one of: {string.Join(", ", Enum.GetNames<Layer>())}.");
    }

    [Theory]
    [MemberData(nameof(SourceProjectNames))]
    public void FolderName_OfEachSourceProject_MatchesProjectName(string projectName)
    {
        ProjectInfo project = SolutionModel.GetSourceProject(projectName);

        Assert.Equal(project.Name, project.FolderName);
    }

    [Theory]
    [MemberData(nameof(SourceProjectNames))]
    public void ProjectName_OfEachSourceProject_UsesLayerPrefix(string projectName)
    {
        ProjectInfo project = SolutionModel.GetSourceProject(projectName);
        string[] allowedPrefixes = project.Layer switch
        {
            Layer.Domains => ["HospitalityOS.Domain."],
            Layer.Adapters => ["HospitalityOS.Adapters.", "HospitalityOS.Integrations."],
            _ => ["HospitalityOS."],
        };

        Assert.True(
            allowedPrefixes.Any(prefix => project.Name.StartsWith(prefix, StringComparison.Ordinal)),
            $"{project.Name} in src/{project.LayerFolder}/ must start with one of: {string.Join(", ", allowedPrefixes)}");
    }

    [Fact]
    public void Solution_WhenLoaded_ContainsEverySourceAndTestProject()
    {
        HashSet<string> solutionPaths = SolutionModel.GetSolutionProjectPaths().ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<string> missing = Directory
            .EnumerateFiles(SolutionModel.RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(path => SolutionModel.NormalizeRelativePath(Path.GetRelativePath(SolutionModel.RepositoryRoot, path)))
            .Where(path => path.StartsWith("src/", StringComparison.Ordinal) || path.StartsWith("tests/", StringComparison.Ordinal))
            .Where(path => !solutionPaths.Contains(path))
            .ToList();

        Assert.True(missing.Count == 0, $"Projects missing from {SolutionModel.SolutionFileName}: {string.Join(", ", missing)}");
    }
}
