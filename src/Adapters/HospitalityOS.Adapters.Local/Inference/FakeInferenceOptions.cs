namespace HospitalityOS.Adapters.Local.Inference;

/// <summary>
/// Scripts for the <c>Fake</c> inference adapter, by name. A fake provider names its script in its settings
/// (<c>Script</c>); register scripts with <c>services.Configure&lt;FakeInferenceOptions&gt;(...)</c> before the host
/// starts (tests and evaluation runs do; the scripts for each workflow ship with that workflow).
/// </summary>
public sealed class FakeInferenceOptions
{
    /// <summary>
    /// Scripts by name (case-insensitive).
    /// </summary>
    public Dictionary<string, FakeScriptOptions> Scripts { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// What a fake model answers. Rules are tried in order and the first that matches answers; when none matches, the fake
/// falls back to its deterministic defaults (see <c>FakeModel</c>).
/// </summary>
public sealed class FakeScriptOptions
{
    /// <summary>
    /// Chat turns.
    /// </summary>
    public IList<FakeChatRuleOptions> Chat { get; } = [];

    /// <summary>
    /// Classifications.
    /// </summary>
    public IList<FakeClassificationRuleOptions> Classification { get; } = [];

    /// <summary>
    /// Extractions.
    /// </summary>
    public IList<FakeExtractionRuleOptions> Extraction { get; } = [];

    /// <summary>
    /// Verdicts.
    /// </summary>
    public IList<FakeJudgementRuleOptions> Judgement { get; } = [];
}

/// <summary>
/// A chat turn: when the conversation matches every condition given (none given matches any turn), reply with text or
/// call a tool.
/// </summary>
/// <param name="WhenUserSays">The last user message contains this (ignoring case).</param>
/// <param name="AfterTool">The last message is the result of a call to this tool.</param>
/// <param name="Reply">The assistant's text; <c>{user}</c> is the last user message and <c>{toolResult}</c> the last tool
/// result.</param>
/// <param name="CallTool">A tool to call instead of replying.</param>
/// <param name="Arguments">The call's arguments, a JSON object (default <c>{}</c>).</param>
public sealed record FakeChatRuleOptions(
    string? WhenUserSays = null,
    string? AfterTool = null,
    string? Reply = null,
    string? CallTool = null,
    string? Arguments = null);

/// <summary>
/// A classification: when the input contains <paramref name="WhenInputContains"/> (ignoring case), choose
/// <paramref name="Label"/>.
/// </summary>
public sealed record FakeClassificationRuleOptions(string WhenInputContains, string Label);

/// <summary>
/// An extraction: when the input contains <paramref name="WhenInputContains"/> (ignoring case), return
/// <paramref name="Output"/> (a JSON value).
/// </summary>
public sealed record FakeExtractionRuleOptions(string WhenInputContains, string Output);

/// <summary>
/// A verdict: when the judged output contains <paramref name="WhenOutputContains"/> (ignoring case), return it.
/// </summary>
public sealed record FakeJudgementRuleOptions(string WhenOutputContains, double Score, bool Passed, string Reasoning);
