using System.Reflection;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// The prompt files one assembly embeds: <c>Prompts/{name}/v{number}.md</c> and the lock file
/// <c>Prompts/prompts.lock.json</c>, embedded under <see cref="ResourcePrefix"/> by <c>Directory.Build.props</c>.
/// </summary>
/// <param name="Origin">Where the files come from (the assembly name), for messages and de-duplication.</param>
/// <param name="Files">Each prompt file: its path below <c>Prompts/</c>, with <c>/</c> separators, and its text.</param>
/// <param name="Lock">The lock file's text, or <c>null</c> when there is none.</param>
internal sealed record PromptSource(string Origin, IReadOnlyList<PromptFile> Files, string? Lock)
{
    public const string ResourcePrefix = "HospitalityOS.Prompts/";
    public const string LockFileName = "prompts.lock.json";

    public static PromptSource FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        List<PromptFile> files = [];
        string? lockText = null;

        foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            // %(RecursiveDir) uses the build machine's separator.
            string path = resource[ResourcePrefix.Length..].Replace('\\', '/');
            string text = Read(assembly, resource);

            if (path == LockFileName)
            {
                lockText = text;
            }
            else
            {
                files.Add(new PromptFile(path, text));
            }
        }

        return new PromptSource(assembly.GetName().Name ?? assembly.FullName ?? "(unnamed assembly)", files, lockText);
    }

    private static string Read(Assembly assembly, string resource)
    {
        using Stream stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource '{resource}' could not be opened.");
        using StreamReader reader = new(stream, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }
}

/// <summary>
/// One embedded prompt file.
/// </summary>
/// <param name="Path">Its path below <c>Prompts/</c>, e.g. <c>concierge.answer/v2.md</c>.</param>
/// <param name="Text">Its text as stored.</param>
internal sealed record PromptFile(string Path, string Text);
