using System.Reflection;
using System.Xml.Linq;

namespace HospitalityOS.ArchitectureTests.Support;

/// <summary>
/// Reads the solution's project files from disk so structure rules can be checked
/// even before projects contain any types.
/// </summary>
internal static class SolutionModel
{
    public const string SolutionFileName = "HospitalityOS.slnx";
    public const string SharedKernelName = "HospitalityOS.SharedKernel";

    private static readonly Lazy<string> repositoryRoot = new(FindRepositoryRoot);
    private static readonly Lazy<IReadOnlyList<ProjectInfo>> sourceProjects = new(LoadSourceProjects);

    public static string RepositoryRoot => repositoryRoot.Value;

    public static string SourceRoot => Path.Combine(RepositoryRoot, "src");

    public static IReadOnlyList<ProjectInfo> SourceProjects => sourceProjects.Value;

    public static ProjectInfo GetSourceProject(string name)
    {
        return SourceProjects.Single(project => project.Name == name);
    }

    public static IEnumerable<Assembly> LoadSourceAssemblies(Func<ProjectInfo, bool> predicate)
    {
        return SourceProjects
            .Where(predicate)
            .Select(project => Assembly.Load(new AssemblyName(project.Name)));
    }

    public static IReadOnlyList<string> GetSolutionProjectPaths()
    {
        XDocument solution = XDocument.Load(Path.Combine(RepositoryRoot, SolutionFileName));

        return solution
            .Descendants("Project")
            .Select(project => (string?)project.Attribute("Path"))
            .OfType<string>()
            .Select(NormalizeRelativePath)
            .ToList();
    }

    public static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"Could not find {SolutionFileName} above {AppContext.BaseDirectory}.");
    }

    private static List<ProjectInfo> LoadSourceProjects()
    {
        return Directory
            .EnumerateFiles(SourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(LoadProject)
            .OrderBy(project => project.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static ProjectInfo LoadProject(string projectPath)
    {
        XDocument document = XDocument.Load(projectPath);
        XElement root = document.Root ?? throw new InvalidOperationException($"{projectPath} has no root element.");
        string projectDirectory = Path.GetDirectoryName(projectPath)!;
        string relativeToSource = Path.GetRelativePath(SourceRoot, projectPath);
        string layerFolder = relativeToSource.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

        List<string> projectReferences = root
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .OfType<string>()
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include.Replace('\\', Path.DirectorySeparatorChar))))
            .ToList();

        List<string> packageReferences = root
            .Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .OfType<string>()
            .ToList();

        return new ProjectInfo(
            Name: Path.GetFileNameWithoutExtension(projectPath),
            FolderName: Path.GetFileName(projectDirectory),
            LayerFolder: layerFolder,
            Layer: Enum.TryParse(layerFolder, ignoreCase: false, out Layer layer) ? layer : null,
            Sdk: (string?)root.Attribute("Sdk") ?? string.Empty,
            ProjectReferences: projectReferences,
            PackageReferences: packageReferences);
    }
}
