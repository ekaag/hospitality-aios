using System.Security.Cryptography;
using System.Text;
using HospitalityOS.Bindings.Inference;
using HospitalityOS.Ports.Inference;
using Xunit;

namespace HospitalityOS.UnitTests.Bindings.Inference;

public sealed class PromptRegistryTests
{
    private const string FirstText = "You are the concierge at {{property}}.\n";
    private const string SecondText = "You are the concierge at {{property}}.\nName your source.\n";

    [Fact]
    public void Find_WithoutANumber_IsTheLatestVersion_WithOne_ThatVersion_AndOtherwiseNull()
    {
        PromptRegistry registry = Registry(Source("Concierge", ("concierge.answer/v1.md", FirstText), ("concierge.answer/v2.md", SecondText)));

        Assert.Empty(registry.Errors);
        Assert.Equal(new Prompt(new PromptVersion("concierge.answer", 2, Sha256(SecondText)), SecondText), registry.Find("concierge.answer"));
        Assert.Equal(new Prompt(new PromptVersion("concierge.answer", 1, Sha256(FirstText)), FirstText), registry.Find("Concierge.Answer", 1));
        Assert.Null(registry.Find("concierge.answer", 3));
        Assert.Null(registry.Find("concierge.improvise"));
    }

    [Fact]
    public void Hash_IsTheSha256OfTheTextWithLfLineEndingsAndNoByteOrderMark_SoCheckoutsAgree()
    {
        PromptRegistry registry = Registry(
            Source("Concierge", ("concierge.answer/v1.md", "\uFEFFLine one\r\nLine two\r\n")),
            Source("Intent", ("intent.classify/v1.md", "Line one\nLine two\n")));

        Prompt crlf = registry.Find("concierge.answer")!;
        Prompt lf = registry.Find("intent.classify")!;

        Assert.Empty(registry.Errors);
        Assert.Equal("Line one\nLine two\n", crlf.Template);
        Assert.Equal(Sha256("Line one\nLine two\n"), crlf.Version.Hash);
        Assert.Equal(lf.Version.Hash, crlf.Version.Hash);
    }

    [Fact]
    public void AnUnlockedVersion_IsAnError_GivingTheLineToAdd()
    {
        PromptRegistry registry = new([
            new PromptSource("Concierge", [new PromptFile("concierge.answer/v1.md", FirstText), new PromptFile("concierge.answer/v2.md", SecondText)], Lock(("concierge.answer@1", Sha256(FirstText)))),
            new PromptSource("Intent", [new PromptFile("intent.classify/v1.md", "Route it.")], null)]);

        Assert.Equal(
            [
                $"Prompt 'concierge.answer@2' in Concierge is not locked. Once it is final, add \"concierge.answer@2\": \"{Sha256(SecondText)}\" to Prompts/prompts.lock.json.",
                $"Prompt 'intent.classify@1' in Intent is not locked. Once it is final, add \"intent.classify@1\": \"{Sha256("Route it.")}\" to Prompts/prompts.lock.json.",
            ],
            registry.Errors);
    }

    [Fact]
    public void AVersionChangedAfterItWasLocked_IsAnError()
    {
        PromptRegistry registry = new([new PromptSource("Concierge", [new PromptFile("concierge.answer/v1.md", SecondText)], Lock(("concierge.answer@1", Sha256(FirstText))))]);

        string error = Assert.Single(registry.Errors);
        Assert.Contains("Prompt 'concierge.answer@1' in Concierge has changed since it was locked", error, StringComparison.Ordinal);
        Assert.Contains($"({Sha256(FirstText)}, now {Sha256(SecondText)})", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("concierge.answer/v3.md", "Prompt 'concierge.answer' has versions 1, 3; versions are numbered from 1 without gaps.")]
    [InlineData("concierge.answer/v0.md", "'Prompts/concierge.answer/v0.md' in Concierge is not named Prompts/{name}/v{number}.md")]
    [InlineData("concierge.answer/V2.md", "'Prompts/concierge.answer/V2.md' in Concierge is not named Prompts/{name}/v{number}.md")]
    [InlineData("Concierge.Answer/v2.md", "'Prompts/Concierge.Answer/v2.md' in Concierge is not named Prompts/{name}/v{number}.md")]
    [InlineData("concierge.answer.md", "'Prompts/concierge.answer.md' in Concierge is not named Prompts/{name}/v{number}.md")]
    [InlineData("concierge/answer/v2.md", "'Prompts/concierge/answer/v2.md' in Concierge is not named Prompts/{name}/v{number}.md")]
    public void AMisnamedFile_OrAGapInTheNumbering_IsAnError(string path, string expected)
    {
        PromptRegistry registry = Registry(Source("Concierge", ("concierge.answer/v1.md", FirstText), (path, SecondText)));

        Assert.Contains(registry.Errors, error => error.StartsWith(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void AnEmptyPrompt_ALockEntryWithoutAFile_AndAMalformedLockFile_AreErrors()
    {
        PromptRegistry empty = Registry(Source("Concierge", ("concierge.answer/v1.md", " \r\n")));
        PromptRegistry orphan = new([new PromptSource("Concierge", [], Lock(("concierge.answer@1", Sha256(FirstText))))]);
        PromptRegistry malformed = new([new PromptSource("Concierge", [], "[\"concierge.answer@1\"]")]);

        Assert.Equal(["Prompt 'concierge.answer@1' in Concierge is empty."], empty.Errors);
        Assert.Equal(["Prompts/prompts.lock.json in Concierge locks 'concierge.answer@1', which has no file."], orphan.Errors);
        Assert.StartsWith("Prompts/prompts.lock.json in Concierge must be a JSON object of", Assert.Single(malformed.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePromptNameEmbeddedInTwoAssemblies_IsAnError_AndTheSameAssemblyTwiceIsNot()
    {
        PromptSource concierge = Source("Concierge", ("concierge.answer/v1.md", FirstText));

        Assert.Equal(
            ["Prompt 'concierge.answer' is embedded in both Concierge and Copy; a prompt has one home."],
            Registry(concierge, Source("Copy", ("concierge.answer/v1.md", FirstText))).Errors);
        Assert.Empty(Registry(concierge, concierge).Errors);
    }

    [Fact]
    public void FromAssembly_ReadsThePromptsAndLockFileTheBuildEmbeds()
    {
        PromptRegistry registry = new([PromptSource.FromAssembly(typeof(PromptRegistryTests).Assembly)]);

        Prompt latest = registry.Find("test.concierge")!;

        Assert.Empty(registry.Errors);
        Assert.Equal("test.concierge@2", latest.Version.ToString());
        Assert.Equal(["property"], latest.Variables);
        Assert.StartsWith("You are the concierge at {{property}}.\nAnswer the guest briefly, using only", latest.Template, StringComparison.Ordinal);
        Assert.Equal(1, registry.Find("test.concierge", 1)!.Version.Number);
    }

    private static PromptRegistry Registry(params PromptSource[] sources) => new(sources);

    // A source whose lock file locks every file it holds as it is.
    private static PromptSource Source(string origin, params (string Path, string Text)[] files) =>
        new(
            origin,
            [.. files.Select(file => new PromptFile(file.Path, file.Text))],
            Lock([.. files.Select(file => ($"{Path.GetFileName(Path.GetDirectoryName(file.Path))}@{Path.GetFileNameWithoutExtension(file.Path).TrimStart('v')}", Sha256(file.Text)))]));

    private static string Lock(params (string Entry, string Hash)[] entries) =>
        "{" + string.Join(",", entries.Select(entry => $"\"{entry.Entry}\": \"{entry.Hash}\"")) + "}";

    private static string Sha256(string text) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart('\uFEFF'))));
}
