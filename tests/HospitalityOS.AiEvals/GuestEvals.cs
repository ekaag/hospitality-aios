using System.Text.Json;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Results;
using Xunit;

namespace HospitalityOS.AiEvals;

/// <summary>
/// Runs each evaluation end to end (spec §36): Alex messages the hotel over HTTP; triage, then the Concierge Controller or
/// the Guest Service Controller, reply through whatever models their tasks are bound to; and the turn is checked
/// deterministically (outcome, capability calls, citations, events in the message's flow, and the scenario's own checks)
/// and graded by the model bound to <c>eval.judge</c>. Without settings all are the Fake model, so CI checks the harness and
/// the platform's side of every scenario; with <see cref="EvalHost.SettingsVariable"/> it evaluates real models, and
/// <see cref="ReportVariable"/> collects the results for comparing them.
/// </summary>
public sealed class GuestEvals
{
    /// <summary>
    /// The environment variable naming a file each result is appended to, as one JSON line.
    /// </summary>
    public const string ReportVariable = "HOSPITALITYOS_EVALS_REPORT";

    private static readonly SemaphoreSlim reportLock = new(1, 1);

    public static TheoryData<int> Scenarios => new(Phase1Scenarios.All.Select(scenario => scenario.Number));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task TheReply_MeetsTheScenario(int number)
    {
        EvalScenario scenario = Phase1Scenarios.All.Single(candidate => candidate.Number == number);
        await using EvalHost host = new(scenario);

        if (scenario.Arrange is not null)
        {
            await scenario.Arrange(host, Cancellation);
        }

        (GuestMessageResponse question, GuestMessageResponse answer) = await host.AskAsync(scenario.Input, Cancellation);
        IReadOnlyList<EventMessage> events = await host.EventsAsync(Cancellation);
        Guid correlation = events.Single(message => message.EventType == "GuestMessageReceived" && message.AggregateId == question.Id.Value).CorrelationId;
        IReadOnlyList<EventMessage> flow = scenario.SettledBy is { } settled
            ? await host.FlowUntilAsync(correlation, settled, Cancellation)
            : [.. events.Where(message => message.CorrelationId == correlation)];
        AITraceResponse? trace = await host.TraceAsync(correlation, scenario.Agent, Cancellation);
        Result<Judgement> judgement = await host.JudgeAsync(new JudgeRequest(scenario.Criteria, scenario.Input, answer.Text, scenario.Reference), Cancellation);
        IReadOnlyList<string> verified = scenario.Verify is null ? [] : await scenario.Verify(host, new EvalTurn(question, answer, trace, flow), Cancellation);

        EvalResult result = EvalResult.Of(scenario, answer, trace, flow, judgement, verified, host.Live);
        TestContext.Current.SendDiagnosticMessage(result.Summary);
        await ReportAsync(result);

        Assert.True(result.Failures.Count == 0, $"{scenario} failed:\n- {string.Join("\n- ", result.Failures)}\n{result.Summary}");
    }

    private static async Task ReportAsync(EvalResult result)
    {
        string? path = Environment.GetEnvironmentVariable(ReportVariable);

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await reportLock.WaitAsync(Cancellation);

        try
        {
            await File.AppendAllTextAsync(path, JsonSerializer.Serialize(result) + Environment.NewLine, Cancellation);
        }
        finally
        {
            reportLock.Release();
        }
    }
}

/// <summary>
/// How a scenario went: what the agent said and did, the judge's verdict, and every check that failed.
/// </summary>
public sealed record EvalResult(
    int Scenario,
    string Name,
    bool Live,
    string? Models,
    string Answer,
    AITraceOutcome? Outcome,
    IReadOnlyList<string> CapabilityCalls,
    IReadOnlyList<SourceReference> Citations,
    double? JudgeScore,
    string? JudgeReasoning,
    IReadOnlyList<string> Failures)
{
    public string Summary =>
        $"{Scenario}. {Name} [{Models}]: {(Failures.Count == 0 ? "PASS" : "FAIL")}\n  answer: {Answer}\n  outcome: {Outcome}; capabilities: {string.Join(", ", CapabilityCalls)}"
        + $"\n  judge: {JudgeScore?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} {JudgeReasoning}";

    public static EvalResult Of(
        EvalScenario scenario,
        GuestMessageResponse answer,
        AITraceResponse? trace,
        IReadOnlyList<EventMessage> flow,
        Result<Judgement> judgement,
        IReadOnlyList<string> verified,
        bool live)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(verified);

        List<string> failures = [];
        string[] calls = trace is null
            ? []
            : [.. trace.Body.CapabilityCalls.Select(call => call.ErrorCode is null ? call.Capability : $"{call.Capability} ({call.ErrorCode})")];
        IReadOnlyList<SourceReference> citations = trace?.Body.Response?.Citations ?? [];

        if (answer.Sender != MessageSender.Concierge)
        {
            failures.Add($"The reply came from {answer.Sender}, not the concierge.");
        }

        if (trace is null)
        {
            failures.Add("There is no AI trace of the turn.");
        }
        else if (!scenario.AllowedOutcomes.Contains(trace.Outcome))
        {
            failures.Add($"The outcome is {trace.Outcome}, not {string.Join(" or ", scenario.AllowedOutcomes)}.");
        }

        failures.AddRange(scenario.ExpectedCapabilities.Where(capability => !calls.Contains(capability)).Select(capability => $"The turn did not call {capability}."));
        failures.AddRange(scenario.ExpectedCitations.Where(source => !citations.Contains(source)).Select(source => $"The answer does not cite {source.Type} {source.Id} version {source.Version}."));
        failures.AddRange(scenario.ExpectedEvents.Where(type => flow.All(message => message.EventType != type)).Select(type => $"The message's flow has no {type}."));
        failures.AddRange((scenario.UnexpectedEvents ?? []).Where(type => flow.Any(message => message.EventType == type)).Select(type => $"The message's flow has a {type}."));
        failures.AddRange(verified);

        if (judgement.IsFailure)
        {
            failures.Add($"The judge could not grade the answer: {judgement.Error.Message}");
        }
        else if (!judgement.Value.Passed)
        {
            failures.Add($"The judge failed the answer ({judgement.Value.Score:0.00}): {judgement.Value.Reasoning}");
        }

        string? models = trace is null
            ? null
            : string.Join(", ", trace.Body.ModelCalls.SelectMany(call => call.Attempts).Where(attempt => attempt.Failure is null).Select(attempt => $"{attempt.Provider}:{attempt.Model}").Distinct());

        if (judgement.IsSuccess)
        {
            models += $"; judge {judgement.Value.Model}";
        }

        return new EvalResult(
            scenario.Number,
            scenario.Name,
            live,
            models,
            answer.Text,
            trace?.Outcome,
            calls,
            citations,
            judgement.IsSuccess ? judgement.Value.Score : null,
            judgement.IsSuccess ? judgement.Value.Reasoning : null,
            failures);
    }
}
