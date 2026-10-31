using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Workflows.Concierge;

namespace HospitalityOS.AiEvals;

/// <summary>
/// One AI evaluation (spec §36): a guest's message at the seeded Aurora Grand, and what a good reply does and must not
/// do. The concierge answers questions; the Guest Service Controller turns requests into work.
/// </summary>
/// <param name="Number">The scenario's number in spec §36.</param>
/// <param name="Name">Its name in spec §36.</param>
/// <param name="Input">What the guest says.</param>
/// <param name="RequiredContext">The property's authoritative data the reply depends on, as the judge is told it.</param>
/// <param name="ExpectedBehavior">What a good reply does.</param>
/// <param name="DisallowedBehavior">What a reply must not do.</param>
/// <param name="Reference">The facts the reply must state, if any; every figure in it (time, price, age, room) must appear
/// in the reply for the Fake judge, and the judge model compares the reply with it.</param>
/// <param name="AllowedOutcomes">How the turn may end.</param>
/// <param name="ExpectedCapabilities">Capabilities the turn must call successfully, as <c>Area.Action@version</c>: tools the
/// model calls, and the reads and actions the agent's own code runs.</param>
/// <param name="ExpectedCitations">Sources the reply must cite (it may cite others it retrieved).</param>
/// <param name="ExpectedEvents">Events the message's flow must contain.</param>
/// <param name="Scripted">How the Fake concierge plays a good answer, for runs without a real model.</param>
/// <param name="Arrange">Changes made before the message, e.g. an administrator updating hours or an earlier request.</param>
/// <param name="Agent">The agent whose trace records the turn.</param>
/// <param name="Triaged">How the Fake triage classifies the guest's messages; without rules it guesses from the labels'
/// descriptions.</param>
/// <param name="Read">How the Fake Guest Service Controller reads the guest's requests.</param>
/// <param name="UnexpectedEvents">Events the message's flow must not contain.</param>
/// <param name="SettledBy">An event the flow is waited for before it is checked, for work that follows the reply.</param>
/// <param name="Verify">Checks of what the turn did that depend on records made during the run, returning each failure.</param>
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
    Func<EvalHost, CancellationToken, Task>? Arrange = null,
    string Agent = Concierge.AnswerTask,
    IReadOnlyList<FakeClassificationRuleOptions>? Triaged = null,
    IReadOnlyList<FakeExtractionRuleOptions>? Read = null,
    IReadOnlyList<string>? UnexpectedEvents = null,
    string? SettledBy = null,
    Func<EvalHost, EvalTurn, CancellationToken, Task<IReadOnlyList<string>>>? Verify = null)
{
    /// <summary>
    /// The events of a question the concierge answered.
    /// </summary>
    public static IReadOnlyList<string> AnsweredEvents { get; } = ["GuestMessageReceived", "GuestMessageTriaged", "AITraceCreated", "GuestMessageSent", "GuestQuestionAnswered"];

    /// <summary>
    /// The events of a request the Guest Service Controller recorded and the router gave to a team member.
    /// </summary>
    public static IReadOnlyList<string> RequestedEvents { get; } =
        ["GuestMessageReceived", "GuestMessageTriaged", "AITraceCreated", "ServiceRequestCreated", "GuestMessageSent", "HumanActionRequested", "HumanActionAssigned"];

    /// <summary>
    /// The events of a reply that changed nothing but the conversation.
    /// </summary>
    public static IReadOnlyList<string> RepliedEvents { get; } = ["GuestMessageReceived", "GuestMessageTriaged", "AITraceCreated", "GuestMessageSent"];

    /// <summary>
    /// What the judge grades the reply against.
    /// </summary>
    public string Criteria =>
        $"The response is a hotel concierge's reply to a guest. The hotel's authoritative information: {RequiredContext}\n"
        + $"Pass only if the response does all of this: {ExpectedBehavior}\n"
        + $"Fail if the response does any of this: {DisallowedBehavior}\n"
        + "Judge only against the hotel's information above, not general knowledge.";

    public override string ToString() => $"{Number}. {Name}";
}

/// <summary>
/// One turn of a scenario as it happened.
/// </summary>
/// <param name="Question">The guest's message.</param>
/// <param name="Answer">The reply.</param>
/// <param name="Trace">The agent's trace of the turn, if there is exactly one.</param>
/// <param name="Flow">The events of the message's flow.</param>
public sealed record EvalTurn(GuestMessageResponse Question, GuestMessageResponse Answer, AITraceResponse? Trace, IReadOnlyList<EventMessage> Flow);
