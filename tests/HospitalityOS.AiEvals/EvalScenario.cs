using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Capabilities;

namespace HospitalityOS.AiEvals;

/// <summary>
/// One AI evaluation (spec §36): a guest's question to the concierge at the seeded Aurora Grand, and what a good answer
/// does and must not do.
/// </summary>
/// <param name="Number">The scenario's number in spec §36.</param>
/// <param name="Name">Its name in spec §36.</param>
/// <param name="Input">What the guest asks.</param>
/// <param name="RequiredContext">The property's authoritative data the answer depends on, as the judge is told it.</param>
/// <param name="ExpectedBehavior">What a good answer does.</param>
/// <param name="DisallowedBehavior">What an answer must not do.</param>
/// <param name="Reference">The facts the answer must state, if any; every figure in it (time, price, age) must appear in the
/// answer for the Fake judge, and the judge model compares the answer with it.</param>
/// <param name="AllowedOutcomes">How the turn may end.</param>
/// <param name="ExpectedCapabilities">Tools the model must call successfully, as <c>Area.Action@version</c>.</param>
/// <param name="ExpectedCitations">Sources the answer must cite (it may cite others it retrieved).</param>
/// <param name="ExpectedEvents">Events the question's flow must contain.</param>
/// <param name="Scripted">How the Fake concierge plays a good answer, for runs without a real model.</param>
/// <param name="Arrange">Changes made before the question, e.g. an administrator updating hours.</param>
public sealed record EvalScenario(
    int Number,
    string Name,
    string Input,
    string RequiredContext,
    string ExpectedBehavior,
    string DisallowedBehavior,
    string? Reference,
    IReadOnlyList<AITraceOutcome> AllowedOutcomes,
    IReadOnlyList<string> ExpectedCapabilities,
    IReadOnlyList<SourceReference> ExpectedCitations,
    IReadOnlyList<string> ExpectedEvents,
    IReadOnlyList<FakeChatRuleOptions> Scripted,
    Func<EvalHost, CancellationToken, Task>? Arrange = null)
{
    /// <summary>
    /// The events of a question the concierge answered.
    /// </summary>
    public static IReadOnlyList<string> AnsweredEvents { get; } = ["GuestMessageReceived", "AITraceCreated", "GuestMessageSent", "GuestQuestionAnswered"];

    /// <summary>
    /// What the judge grades the answer against.
    /// </summary>
    public string Criteria =>
        $"The response is a hotel concierge's answer to a guest. The hotel's authoritative information: {RequiredContext}\n"
        + $"Pass only if the response does all of this: {ExpectedBehavior}\n"
        + $"Fail if the response does any of this: {DisallowedBehavior}\n"
        + "Judge only against the hotel's information above, not general knowledge.";

    public override string ToString() => $"{Number}. {Name}";
}
