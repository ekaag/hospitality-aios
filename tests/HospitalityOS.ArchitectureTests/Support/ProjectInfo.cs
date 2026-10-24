namespace HospitalityOS.ArchitectureTests.Support;

/// <summary>
/// A source project as declared in its <c>.csproj</c> file.
/// </summary>
/// <param name="Name">Project file name without extension (also the assembly name).</param>
/// <param name="FolderName">Name of the folder that contains the project file.</param>
/// <param name="LayerFolder">Name of the <c>src/</c> sub-folder the project lives in.</param>
/// <param name="Layer">Layer resolved from <paramref name="LayerFolder"/>, or <c>null</c> if the folder is not a known layer.</param>
/// <param name="Sdk">Value of the project's <c>Sdk</c> attribute.</param>
/// <param name="ProjectReferences">Full paths of referenced project files.</param>
/// <param name="PackageReferences">Referenced NuGet package IDs.</param>
internal sealed record ProjectInfo(
    string Name,
    string FolderName,
    string LayerFolder,
    Layer? Layer,
    string Sdk,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences);
